using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper;

public static class Extensions
{
    public static BehaviorTree? GetBehaviorTree(this Agent agent)
    {
        // Single dict lookup (deep-review 2026-05-24 E6) — was ContainsKey + indexer.
        var logic = BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic;
        if (logic == null)
            return null;
        return logic.trees.TryGetValue(agent, out var tree) ? tree : null;
    }
}
