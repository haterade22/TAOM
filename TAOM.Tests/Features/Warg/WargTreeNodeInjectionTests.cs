using System.Runtime.Serialization;
using BehaviorTrees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.Warg;
using TAOM.Features.Warg.BehaviorTreeElements;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// The warg tree's service nodes take their services through their constructors from
/// WargBehaviorTree.BuildTree (maintainer decision 2026-09-24, #659). These tests drive two of them
/// with substitutes, so a constructor that stops storing a service fails here rather than as a
/// NullReferenceException on the first bite, which the tree's catch turns into a tree that stops
/// running for the rest of the battle. The agents are bare uninitialized Agent objects: the nodes
/// only pass them to the adapter factory, and nothing here reaches the engine.
/// </summary>
[TestClass]
public class WargTreeNodeInjectionTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static WargAttackTask AttackTask(IMissionAdapterFactory factory, IWargAttackService service, Agent warg, int rage) =>
        new WargAttackTask(factory, service)
        {
            Agent = new BTBlackboardValue<Agent>(warg),
            RageAttackAmount = new BTBlackboardValue<int>(rage),
        };

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void WargAttackTask_Execute_AttacksWithTheInjectedServiceThroughTheInjectedFactory()
    {
        var factory = Substitute.For<IMissionAdapterFactory>();
        var service = Substitute.For<IWargAttackService>();
        Agent warg = BareAgent();
        IAgentAdapter wargAdapter = Substitute.For<IAgentAdapter>();
        factory.GetAgentAdapter(warg).Returns(wargAdapter);
        WargAttackTask task = AttackTask(factory, service, warg, rage: 3);

        BTTaskStatus status = task.Execute();

        Assert.AreEqual(BTTaskStatus.FinishedWithTrue, status);
        service.Received(1).WargAttack(wargAdapter);
        Assert.AreEqual(2, task.RageAttackAmount.GetValue());
    }

    [TestMethod]
    public void WargAttackTask_ExecuteWithNoWarg_AttacksNothingAndStillFinishes()
    {
        var factory = Substitute.For<IMissionAdapterFactory>();
        var service = Substitute.For<IWargAttackService>();
        WargAttackTask task = AttackTask(factory, service, warg: null, rage: 3);

        BTTaskStatus status = task.Execute();

        Assert.AreEqual(BTTaskStatus.FinishedWithTrue, status);
        service.DidNotReceiveWithAnyArgs().WargAttack(default);
        Assert.AreEqual(2, task.RageAttackAmount.GetValue());
    }

    [TestCategory("RequiresGame")]
    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void WargAiControlledIsNotFacingEnemy_Evaluate_NegatesTheHitByAgentsLikelyHitTest(bool likelyToHit, bool expected)
    {
        var factory = Substitute.For<IMissionAdapterFactory>();
        Agent warg = BareAgent();
        Agent hitBy = BareAgent();
        IAgentAdapter wargAdapter = Substitute.For<IAgentAdapter>();
        IAgentAdapter hitByAdapter = Substitute.For<IAgentAdapter>();
        factory.GetAgentAdapter(warg).Returns(wargAdapter);
        factory.GetAgentAdapter(hitBy).Returns(hitByAdapter);
        hitByAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange).Returns(likelyToHit);
        var decorator = new WargAiControlledIsNotFacingEnemy(factory)
        {
            Agent = new BTBlackboardValue<Agent>(warg),
            AgentHitBy = new BTBlackboardValue<Agent>(hitBy),
        };

        Assert.AreEqual(expected, decorator.Evaluate());
        hitByAdapter.Received(1).IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
    }
}
