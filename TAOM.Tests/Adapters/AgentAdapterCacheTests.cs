using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The cache behind <see cref="MissionAdapterFactory"/>. It used to be keyed by <c>Agent.Index</c>,
/// and the engine recycles indices within a mission once an agent is deleted, so a late-spawned
/// horse could be handed a dead warg's adapter, get a warg behavior tree, and ask the engine to
/// play <c>act_warg_attack_running</c> on <c>as_horse</c> (player freeze 2026-09-13, #592).
/// Identity is the agent object itself; indices are kept only to count how often the engine
/// reuses one (<c>Evict</c> frees an index, <c>NoteBuilt</c> reports the build that lands on it).
/// </summary>
[TestClass]
public class AgentAdapterCacheTests
{
    private static IAgentAdapter NewAdapter() => Substitute.For<IAgentAdapter>();

    /// <summary>A key whose Equals says every instance is equal: the cache must not believe it.</summary>
    private sealed class AlwaysEqualKey
    {
        public override bool Equals(object obj) => obj is AlwaysEqualKey;
        public override int GetHashCode() => 0;
    }

    [TestMethod]
    public void TryGet_UnknownAgent_ReturnsFalse()
    {
        var cache = new AgentAdapterCache();

        Assert.IsFalse(cache.TryGet(new object(), out IAgentAdapter adapter));
        Assert.IsNull(adapter);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void TryGet_NullAgent_ReturnsFalse()
    {
        var cache = new AgentAdapterCache();

        Assert.IsFalse(cache.TryGet(null, out _));
    }

    [TestMethod]
    public void Add_ThenTryGet_ReturnsTheSameAdapter()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        var adapter = NewAdapter();

        cache.Add(agent, adapter);

        Assert.IsTrue(cache.TryGet(agent, out IAgentAdapter found));
        Assert.AreSame(adapter, found);
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public void Add_TwoAgents_KeepsBothApart()
    {
        var cache = new AgentAdapterCache();
        var deadWarg = new object();
        var horseInItsSlot = new object();
        var wargAdapter = NewAdapter();
        var horseAdapter = NewAdapter();

        cache.Add(deadWarg, wargAdapter);
        cache.Add(horseInItsSlot, horseAdapter);

        cache.TryGet(deadWarg, out IAgentAdapter a);
        cache.TryGet(horseInItsSlot, out IAgentAdapter b);
        Assert.AreSame(wargAdapter, a);
        Assert.AreSame(horseAdapter, b);
        Assert.AreEqual(2, cache.Count);
    }

    [TestMethod]
    public void Add_ComparesKeysByReference_NotByEquals()
    {
        var cache = new AgentAdapterCache();
        var a = new AlwaysEqualKey();
        var b = new AlwaysEqualKey();
        var first = NewAdapter();
        var second = NewAdapter();

        cache.Add(a, first);
        cache.Add(b, second);

        Assert.AreEqual(2, cache.Count, "two objects that claim equality are still two agents");
        cache.TryGet(a, out IAgentAdapter foundA);
        cache.TryGet(b, out IAgentAdapter foundB);
        Assert.AreSame(first, foundA);
        Assert.AreSame(second, foundB);
    }

    [TestMethod]
    public void Add_SameAgentTwice_ReplacesTheEntry()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        var replacement = NewAdapter();
        cache.Add(agent, NewAdapter());

        cache.Add(agent, replacement);

        cache.TryGet(agent, out IAgentAdapter found);
        Assert.AreSame(replacement, found);
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public void Evict_DropsTheAgent()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        cache.Add(agent, NewAdapter());

        Assert.IsTrue(cache.Evict(agent, 5));

        Assert.IsFalse(cache.TryGet(agent, out _));
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void Evict_UnknownAgent_ReturnsFalse_ButStillRemembersTheFreedIndex()
    {
        var cache = new AgentAdapterCache();

        Assert.IsFalse(cache.Evict(new object(), 9));

        Assert.IsTrue(cache.NoteBuilt(9), "a deleted agent frees its index whether or not it was ever adapted");
    }

    [TestMethod]
    public void Evict_NullAgent_ReturnsFalse_ButStillRemembersTheFreedIndex()
    {
        var cache = new AgentAdapterCache();

        Assert.IsFalse(cache.Evict(null, 3));

        Assert.IsTrue(cache.NoteBuilt(3));
    }

    [TestMethod]
    public void NoteBuilt_OnAnIndexNobodyFreed_ReportsNoReuse()
    {
        var cache = new AgentAdapterCache();

        Assert.IsFalse(cache.NoteBuilt(12));
        Assert.AreEqual(0, cache.ReuseCount);
    }

    [TestMethod]
    public void NoteBuilt_OnTheIndexOfAnEvictedAgent_ReportsReuseOnce()
    {
        var cache = new AgentAdapterCache();
        var warg = new object();
        cache.Add(warg, NewAdapter());
        cache.Evict(warg, 41);

        bool firstBuild = cache.NoteBuilt(41);
        bool secondBuildWithoutDeletion = cache.NoteBuilt(41);

        Assert.IsTrue(firstBuild);
        Assert.IsFalse(secondBuildWithoutDeletion, "one deletion is one reuse");
        Assert.AreEqual(1, cache.ReuseCount);
    }

    [TestMethod]
    public void ReuseCount_AccumulatesAcrossDeletions()
    {
        var cache = new AgentAdapterCache();
        cache.Evict(new object(), 2);
        cache.NoteBuilt(2);
        cache.Evict(new object(), 2);

        Assert.IsTrue(cache.NoteBuilt(2));
        Assert.AreEqual(2, cache.ReuseCount);
    }

    [TestMethod]
    public void Clear_EmptiesTheCache_AndForgetsFreedIndicesAndTheCount()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        cache.Add(agent, NewAdapter());
        cache.Evict(agent, 4);
        cache.NoteBuilt(4);
        cache.Evict(new object(), 7);

        cache.Clear();

        Assert.AreEqual(0, cache.Count);
        Assert.AreEqual(0, cache.ReuseCount);
        Assert.IsFalse(cache.NoteBuilt(7), "a new mission starts with no freed indices");
    }

    [TestMethod]
    public void Add_NullAgent_Throws()
    {
        var cache = new AgentAdapterCache();

        Assert.ThrowsException<ArgumentNullException>(() => cache.Add(null, NewAdapter()));
    }

    [TestMethod]
    public void Add_NullAdapter_Throws()
    {
        var cache = new AgentAdapterCache();

        Assert.ThrowsException<ArgumentNullException>(() => cache.Add(new object(), null));
    }

    [TestMethod]
    public void ConcurrentUse_FromSeveralThreads_NeverThrows_AndEndsConsistent()
    {
        // The engine's built and deleted callbacks reach the cache from the main thread while code left
        // on the asynchronous agent tick may still read it (#592); the lock is what makes that safe.
        var cache = new AgentAdapterCache();
        var agents = new object[64];
        for (int i = 0; i < agents.Length; i++) agents[i] = new object();
        var threads = new System.Threading.Thread[8];
        System.Exception failure = null;
        for (int t = 0; t < threads.Length; t++)
        {
            int seed = t;
            threads[t] = new System.Threading.Thread(() =>
            {
                try
                {
                    for (int n = 0; n < 2000; n++)
                    {
                        object agent = agents[(n * 7 + seed) % agents.Length];
                        switch (n % 4)
                        {
                            case 0: cache.Add(agent, NewAdapter()); break;
                            case 1: cache.TryGet(agent, out _); break;
                            case 2: cache.Evict(agent, n % 16); break;
                            default: cache.NoteBuilt(n % 16); break;
                        }
                    }
                }
                catch (System.Exception e) { failure = e; }
            });
        }
        foreach (var th in threads) th.Start();
        foreach (var th in threads) th.Join();

        Assert.IsNull(failure, failure?.ToString());
        Assert.IsTrue(cache.Count >= 0 && cache.Count <= agents.Length);
        cache.Clear();
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void GetOrAdd_ReturnsTheHeldAdapter_WithoutBuilding()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        var held = NewAdapter();
        cache.Add(agent, held);
        int built = 0;

        var got = cache.GetOrAdd(agent, _ => { built++; return NewAdapter(); });

        Assert.AreSame(held, got);
        Assert.AreEqual(0, built);
    }

    // Codex review 109 (2026-09-13): TryGet then Add let two concurrent misses wrap one agent twice.
    [TestMethod]
    public void GetOrAdd_UnderContention_BuildsOnce_AndEveryCallerGetsTheSameAdapter()
    {
        var cache = new AgentAdapterCache();
        var agent = new object();
        int built = 0;
        var results = new IAgentAdapter[16];
        var threads = new System.Threading.Thread[16];
        using var start = new System.Threading.ManualResetEventSlim(false);
        for (int i = 0; i < threads.Length; i++)
        {
            int slot = i;
            threads[i] = new System.Threading.Thread(() =>
            {
                start.Wait();
                results[slot] = cache.GetOrAdd(agent, _ =>
                {
                    System.Threading.Interlocked.Increment(ref built);
                    return NewAdapter();
                });
            });
            threads[i].Start();
        }
        start.Set();
        foreach (var thread in threads) thread.Join();

        Assert.AreEqual(1, built, "two concurrent misses must not both build");
        foreach (var result in results) Assert.AreSame(results[0], result);
        Assert.AreEqual(1, cache.Count);
    }
}
