using System;
using BehaviorTrees;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper;

public class BehaviorTreeAgentComponent : AgentComponent
{
    // Shared across all BT agents (deep-review 2026-05-24 E7) — was `new Random()`
    // per construction, which allocated a backing array per agent.
    private static readonly Random SharedRandom = new Random();

    private float timeSinceLastEvaluation;

    public BehaviorTree? Tree { get; private set; }

    /// <summary>True while <see cref="BehaviorTreeMissionLogic"/> ticks this component.</summary>
    internal bool IsScheduled { get; set; }

    public BehaviorTreeAgentComponent(Agent agent, string treeName, params object[] args)
        : base(agent)
    {
        var array = new object[args.Length + 1];
        array[0] = agent;
        Array.Copy(args, 0, array, 1, args.Length);
        args = array;
        Tree = BehaviorTreeBannerlordWrapper.Instance.AddBehaviorTree(treeName, args);
        if (Tree != null)
        {
            timeSinceLastEvaluation = (float)(SharedRandom.NextDouble() * (Tree._rootEvaluationDelay / 1000f));
            BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic?.Schedule(this);
        }
    }

    // Mission.OnAgentRemoved calls this through Agent.OnRemove after every behavior's OnAgentRemoved,
    // on whichever thread native raised the removal; a player's log caught one off the main thread (#634). The
    // schedule list and tree map belong to the mission tick, so the cleanup queues behind the logic's
    // own parked callbacks for this removal. No logic means no mission, and nothing left to dispose.
    public override void OnAgentRemoved() =>
        BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic?.RunOnMissionThread(
            "BehaviorTreeAgentComponent.OnAgentRemoved", Retire);

    private void Retire()
    {
        BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic?.Unschedule(this);
        BehaviorTreeBannerlordWrapper.Instance.DisposeTree(base.Agent);
    }

    // The engine calls this from Agent.Tick, which single-player runs on an asynchronous AI thread
    // (MissionState.cs:201 -> Mission.TickAgentsAndTeams, a native callback; v1.4.8). A tree ticked
    // there registered blows and read the tree logic's collections while the main thread's
    // agent-removed callbacks wrote them (#592, two player freezes). Deliberately empty:
    // BehaviorTreeMissionLogic.OnMissionTick drives TickOnMissionThread on the main thread instead,
    // in the managed mission-tick phase where the engine's own behaviors run.
    public override void OnTick(float dt) { }

    /// <summary>The tick <see cref="BehaviorTreeMissionLogic"/> drives; same cadence rule as before.</summary>
    internal void TickOnMissionThread(float dt)
    {
        // The engine's IsActive() answers for whoever occupies the agent's slot; a component left
        // scheduled for a deleted agent must not run its tree against the slot's new tenant (#592).
        if (Tree == null || !Agent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(Agent)) return;
        timeSinceLastEvaluation += dt;
        if ((Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation || Tree.ShouldRunNextTick)
        {
            Tree.RunTree();
            timeSinceLastEvaluation = 0f;
        }
    }
}
