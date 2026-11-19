using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The side <see cref="AgentAdapter.IsSameTeam"/> compares (#594): an agent's own team, or, for a mount the engine
/// gave none, its last rider's. Bare agents: the adapter constructor stores the reference and touches nothing, and
/// IsSameTeam and HasKnownSide read only <c>Agent.Team</c> and the ledger.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class AgentAdapterSideTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
    private static Team BareTeam() => (Team)FormatterServices.GetUninitializedObject(typeof(Team));

    private static Agent AgentOn(Team team)
    {
        var agent = BareAgent();
        typeof(Agent).GetProperty(nameof(Agent.Team))!.GetSetMethod(nonPublic: true)!.Invoke(agent, new object[] { team });
        return agent;
    }

    private static AgentAdapter Adapt(Agent agent) =>
        new AgentAdapter(agent, Substitute.For<IMissionAdapterFactory>(), Substitute.For<IModLogger>(),
            () => Substitute.For<IBoneCollisionService>());

    [TestMethod]
    public void IsSameTeam_LooseMountWhoseLastRiderWasOnTheSoldiersTeam_ReturnsTrue()
    {
        var team = BareTeam();
        var soldier = Adapt(AgentOn(team));
        var mountAgent = BareAgent();
        MountRiderSides.Record(mountAgent, team);
        var mount = Adapt(mountAgent);

        Assert.IsTrue(soldier.IsSameTeam(mount), "the soldier sees the mount on his side");
        Assert.IsTrue(mount.IsSameTeam(soldier), "and the mount sees the soldier on its side");
    }

    [TestMethod]
    public void IsSameTeam_LooseMountWhoseLastRiderWasOnAnotherTeam_ReturnsFalse()
    {
        var soldier = Adapt(AgentOn(BareTeam()));
        var mountAgent = BareAgent();
        MountRiderSides.Record(mountAgent, BareTeam());
        Assert.IsFalse(soldier.IsSameTeam(Adapt(mountAgent)));
    }

    [TestMethod]
    public void IsSameTeam_LooseMountNeverRidden_ReturnsFalse()
        => Assert.IsFalse(Adapt(AgentOn(BareTeam())).IsSameTeam(Adapt(BareAgent())));

    [TestMethod]
    public void IsSameTeam_TwoAgentsOnOneTeam_ReturnsTrue()
    {
        var team = BareTeam();
        Assert.IsTrue(Adapt(AgentOn(team)).IsSameTeam(Adapt(AgentOn(team))));
    }

    [TestMethod]
    public void HasKnownSide_LooseMountNeverRidden_ReturnsFalse()
        => Assert.IsFalse(Adapt(BareAgent()).HasKnownSide);

    [TestMethod]
    public void HasKnownSide_LooseMountWithARecordedRider_ReturnsTrue()
    {
        var mountAgent = BareAgent();
        MountRiderSides.Record(mountAgent, BareTeam());
        Assert.IsTrue(Adapt(mountAgent).HasKnownSide);
    }

    [TestMethod]
    public void HasKnownSide_AgentWithItsOwnTeam_ReturnsTrue()
        => Assert.IsTrue(Adapt(AgentOn(BareTeam())).HasKnownSide);
}
