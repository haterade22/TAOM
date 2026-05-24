using System;
using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper;

public class BehaviorTreeAgentComponent : AgentComponent
{
    // Shared across all BT agents (deep-review 2026-05-24 E7) — was `new Random()`
    // per construction, which allocated a backing array per agent.
    private static readonly Random SharedRandom = new Random();

    private float timeSinceLastEvaluation;

    public BehaviorTree? Tree { get; private set; }

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
        }
    }

    public override void OnAgentRemoved()
    {
        BehaviorTreeBannerlordWrapper.Instance.DisposeTree(base.Agent);
    }

    // OnTick (was OnTickAsAI in v1.3.x; v1.4.5 renamed to OnTick).
    public override void OnTick(float dt)
    {
        if (Tree != null)
        {
            timeSinceLastEvaluation += dt;
            if ((Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation || Tree.ShouldRunNextTick)
            {
                Tree.RunTree();
                timeSinceLastEvaluation = 0f;
            }
        }
    }
}
