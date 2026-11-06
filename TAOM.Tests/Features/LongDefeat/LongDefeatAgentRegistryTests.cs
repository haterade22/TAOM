using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The role and felled mark of every attacker the field spawned (phase2-plan.md section 1): keys that only go up, agents by reference, one lock, evicted at removal.</summary>
[TestClass]
public class LongDefeatAgentRegistryTests
{
    [TestMethod]
    public void Register_GivesKeysThatOnlyGoUp_AndTheSameKeyForTheSameAgent()
    {
        var registry = new LongDefeatAgentRegistry();
        var a = new object();
        var b = new object();

        var first = registry.Register(a, AttackerRole.Roster);
        var second = registry.Register(b, AttackerRole.Summoned);
        var again = registry.Register(a, AttackerRole.Commander);

        Assert.AreEqual(first, again, "the same agent keeps its key");
        Assert.IsTrue(second.Value > first.Value);
        Assert.AreEqual(AttackerRole.Roster, registry.RoleOf(a), "a second registration does not change the first role");
        Assert.AreEqual(2, registry.Count);
    }

    [TestMethod]
    public void AgentsAreHeldByReference_NotByEquality()
    {
        var registry = new LongDefeatAgentRegistry();
        var one = new string('x', 3);
        var other = new string('x', 3);

        var k1 = registry.Register(one, AttackerRole.Roster);
        var k2 = registry.Register(other, AttackerRole.Commander);

        Assert.AreNotEqual(k1, k2, "two equal strings are two agents");
        Assert.AreEqual(AttackerRole.Commander, registry.RoleOf(other));
    }

    [TestMethod]
    public void TakeRemoval_ReturnsTheRoleAndTheFelledMark_AndEvicts()
    {
        var registry = new LongDefeatAgentRegistry();
        var felled = new object();
        var killed = new object();
        registry.Register(felled, AttackerRole.Summoned);
        registry.Register(killed, AttackerRole.Commander);
        Assert.IsTrue(registry.MarkFelled(felled));

        Assert.IsTrue(registry.TakeRemoval(felled, out var role1, out var isFelled1, out _));
        Assert.IsTrue(registry.TakeRemoval(killed, out var role2, out var isFelled2, out _));

        Assert.AreEqual((AttackerRole.Summoned, true), (role1, isFelled1));
        Assert.AreEqual((AttackerRole.Commander, false), (role2, isFelled2));
        Assert.AreEqual(0, registry.Count);
        Assert.IsFalse(registry.TakeRemoval(felled, out _, out _, out _), "an agent is removed once");
    }

    [TestMethod]
    public void AnUnknownAgent_IsARosterSoldierNeverFelled_AndCannotBeMarked()
    {
        var registry = new LongDefeatAgentRegistry();
        var stranger = new object();

        Assert.AreEqual(AttackerRole.Roster, registry.RoleOf(stranger));
        Assert.IsFalse(registry.IsFelled(stranger));
        Assert.IsFalse(registry.MarkFelled(stranger));
        Assert.IsFalse(registry.TakeRemoval(stranger, out var role, out var felled, out _));
        Assert.AreEqual((AttackerRole.Roster, false), (role, felled));
        Assert.IsFalse(registry.TryGetKey(null, out _));
    }

    [TestMethod]
    public void AKeyIsNeverReused_AfterTheAgentIsEvicted_OrTheRegistryCleared()
    {
        var registry = new LongDefeatAgentRegistry();
        var a = new object();
        var first = registry.Register(a, AttackerRole.Roster);
        registry.TakeRemoval(a, out _, out _, out _);

        var second = registry.Register(new object(), AttackerRole.Roster);
        registry.Clear();
        var third = registry.Register(new object(), AttackerRole.Roster);

        Assert.IsTrue(second.Value > first.Value && third.Value > second.Value);
        Assert.AreEqual(1, registry.Count);
    }

    [TestMethod]
    public void Snapshot_IsACopy_SafeToWalkWhileTheEngineRemovesAgents()
    {
        var registry = new LongDefeatAgentRegistry();
        var agents = Enumerable.Range(0, 5).Select(_ => new object()).ToList();
        foreach (var agent in agents)
            registry.Register(agent, AttackerRole.Roster);
        registry.MarkFelled(agents[2]);

        var snapshot = registry.Snapshot();
        foreach (var agent in agents)
            registry.TakeRemoval(agent, out _, out _, out _);

        Assert.AreEqual(5, snapshot.Count);
        Assert.AreEqual(1, snapshot.Count(s => s.Felled));
        Assert.AreEqual(0, registry.Count);
    }

    [TestMethod]
    public void ManyThreadsRegistering_GetUniqueKeys()
    {
        var registry = new LongDefeatAgentRegistry();
        var keys = new List<AgentKey>();

        Parallel.For(0, 200, _ =>
        {
            var key = registry.Register(new object(), AttackerRole.Roster);
            lock (keys)
                keys.Add(key);
        });

        Assert.AreEqual(200, keys.Distinct().Count());
        Assert.AreEqual(200, registry.Count);
    }

    [TestMethod]
    public void TryGetAgent_FindsARegisteredAgentByItsKey_AndNotAnEvictedOneOrAStrangersKey()
    {
        var registry = new LongDefeatAgentRegistry();
        var a = new object();
        var key = registry.Register(a, AttackerRole.Roster);

        Assert.IsTrue(registry.TryGetAgent(key, out var found));
        Assert.AreSame(a, found);
        Assert.IsFalse(registry.TryGetAgent(new AgentKey(key.Value + 100), out _));
        registry.TakeRemoval(a, out _, out _, out _);
        Assert.IsFalse(registry.TryGetAgent(key, out _), "an evicted agent is no longer found by its key");
    }

    [TestMethod]
    public void TakeRemoval_AlsoReturnsTheKey_SoTheRunCanForgetTheAgent()
    {
        var registry = new LongDefeatAgentRegistry();
        var a = new object();
        var key = registry.Register(a, AttackerRole.Roster);

        Assert.IsTrue(registry.TakeRemoval(a, out _, out _, out var removed));
        Assert.AreEqual(key, removed);
        Assert.IsFalse(registry.TakeRemoval(a, out _, out _, out var again));
        Assert.AreEqual(0, again.Value, "no key for an agent the registry does not hold");
    }

    [TestMethod]
    public void ADefenderRegisteredOnItsFirstHit_IsHeldLikeAnyOtherAgent()
    {
        // The poison victims are defenders; they register when first struck and are evicted at their removal, so a long run holds no more than the agents that stand or were hit.
        var registry = new LongDefeatAgentRegistry();
        var defender = new object();

        var first = registry.Register(defender, AttackerRole.Roster);
        var second = registry.Register(defender, AttackerRole.Roster);

        Assert.AreEqual(first, second);
        Assert.IsTrue(registry.TryGetKey(defender, out var key) && key == first);
        Assert.AreEqual(1, registry.Count);
    }
}
