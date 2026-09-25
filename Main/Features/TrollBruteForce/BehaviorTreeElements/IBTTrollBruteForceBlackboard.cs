using BehaviorTrees;

namespace TAOM.Features.TrollBruteForce.BehaviorTreeElements;

/// <summary>
/// The troll tree's blackboard: when the smash last started, in mission seconds (null = never). The builder
/// reflection-copies it from the tree onto every node implementing this interface.
/// </summary>
public interface IBTTrollBruteForceBlackboard : IBTBlackboard
{
    BTBlackboardValue<float?> LastFired { get; set; }
}
