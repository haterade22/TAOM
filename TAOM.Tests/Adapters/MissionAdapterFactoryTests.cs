using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Adapters;

/// <summary>
/// End-to-end pin on the factory boundary: two distinct engine agents that happen to share an
/// index (the engine recycles indices within a mission) must never share an adapter. The agents
/// are bare uninitialized <see cref="Agent"/> objects: the adapter constructor stores the reference
/// and touches nothing on it, and <c>Agent.Index</c> is a plain auto-property reading its own field,
/// so nothing here reaches the engine's static initializers.
/// </summary>
[TestClass]
public class MissionAdapterFactoryTests
{
    private static MissionAdapterFactory NewFactory() =>
        new MissionAdapterFactory(Substitute.For<IModLogger>(), () => Substitute.For<IBoneCollisionService>());

    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    [TestMethod]
    public void GetAgentAdapter_TwoAgentsWithTheSameIndex_GetDistinctAdapters()
    {
        var factory = NewFactory();
        var deadWarg = BareAgent();
        var horseInItsSlot = BareAgent();
        Assert.AreEqual(deadWarg.Index, horseInItsSlot.Index, "both bare agents report the default index");

        var wargAdapter = factory.GetAgentAdapter(deadWarg);
        var horseAdapter = factory.GetAgentAdapter(horseInItsSlot);

        Assert.AreNotSame(wargAdapter, horseAdapter);
        Assert.AreSame(wargAdapter, factory.GetAgentAdapter(deadWarg));
    }

    [TestMethod]
    public void Evict_ThenGetAgentAdapter_BuildsAFreshAdapter()
    {
        var factory = NewFactory();
        var agent = BareAgent();
        var original = factory.GetAgentAdapter(agent);

        factory.Evict(agent);

        Assert.AreNotSame(original, factory.GetAgentAdapter(agent));
    }

    [TestMethod]
    public void OnAgentBuilt_OnAFreedIndex_LogsTheFirstReuseOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var factory = new MissionAdapterFactory(logger, () => Substitute.For<IBoneCollisionService>());
        var deleted = BareAgent();
        factory.Evict(deleted);

        factory.OnAgentBuilt(BareAgent());     // lands on the freed index: first reuse, logged
        factory.Evict(BareAgent());
        factory.OnAgentBuilt(BareAgent());     // second reuse: counted, not logged

        logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("[AdapterCache]") && s.Contains("reused")));
    }

    [TestMethod]
    public void OnAgentBuilt_OnAnIndexNobodyFreed_LogsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var factory = new MissionAdapterFactory(logger, () => Substitute.For<IBoneCollisionService>());

        factory.OnAgentBuilt(BareAgent());

        logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ClearCache_ForgetsFreedIndices_SoTheNextMissionLogsItsOwnFirstReuse()
    {
        var logger = Substitute.For<IModLogger>();
        var factory = new MissionAdapterFactory(logger, () => Substitute.For<IBoneCollisionService>());
        factory.Evict(BareAgent());
        factory.OnAgentBuilt(BareAgent());
        factory.ClearCache();
        logger.ClearReceivedCalls();

        factory.OnAgentBuilt(BareAgent());     // nothing freed this mission
        factory.Evict(BareAgent());
        factory.OnAgentBuilt(BareAgent());     // this mission's first reuse

        logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("reused")));
    }

    [TestMethod]
    public void NullAgents_AreNoOps()
    {
        var factory = NewFactory();

        Assert.IsNull(factory.GetAgentAdapter(null));
        factory.Evict(null);
        factory.OnAgentBuilt(null);
    }
}
