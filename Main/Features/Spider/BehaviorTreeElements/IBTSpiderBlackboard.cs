using System;
using BehaviorTrees;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Shared blackboard state for the spider behavior tree (cooldown stamps + target bearing).
/// The builder reflection-copies these properties from the tree onto every node implementing this interface.
/// Mirrors <c>IBTElephantBlackboard</c>.
/// </summary>
public interface IBTSpiderBlackboard : IBTBlackboard
{
    /// <summary>When the pounce last fired; null = never (off cooldown).</summary>
    BTBlackboardValue<DateTime?> PounceLastFired { get; set; }

    /// <summary>When a side (left/right) swipe last fired; null = never (off cooldown).</summary>
    BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }

    /// <summary>
    /// Signed bearing of the best in-range enemy, written by <see cref="SpiderEngageDecorator"/> each passing
    /// scan: the z of cross(lookDir, toEnemy) — POSITIVE = enemy on the spider's LEFT (counter-clockwise, Z-up
    /// right-handed), NEGATIVE = RIGHT. Consumed by <see cref="SpiderSideAttackTask"/> to pick the matching swipe
    /// clip. (If in-game testing shows mirrored swipes, swap the clip constants in SpiderConfig — not this sign.)
    /// </summary>
    BTBlackboardValue<float> TargetBearing { get; set; }
}
