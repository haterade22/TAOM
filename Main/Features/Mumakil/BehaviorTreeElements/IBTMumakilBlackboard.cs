using System;
using BehaviorTrees;

namespace TAOM.Features.Mumakil.BehaviorTreeElements;

/// <summary>
/// Shared blackboard state for the Mûmakil behavior tree (cooldown stamps + target bearing).
/// The builder reflection-copies these properties from the tree onto every node implementing this interface.
/// </summary>
public interface IBTMumakilBlackboard : IBTBlackboard
{
    /// <summary>When the trample last fired; null = never (off cooldown).</summary>
    BTBlackboardValue<DateTime?> TrampleLastFired { get; set; }

    /// <summary>When a side (left/right tusk-swing) attack last fired; null = never (off cooldown).</summary>
    BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }

    /// <summary>
    /// Signed bearing of the best in-range enemy, written by <see cref="MumakilEngageDecorator"/> each
    /// passing scan: the z of cross(lookDir, toEnemy) — POSITIVE = enemy on the Mûmakil's LEFT (counter-clockwise,
    /// Z-up right-handed), NEGATIVE = RIGHT. Consumed by <see cref="MumakilSideAttackTask"/> to pick the matching
    /// swing clip. (If in-game testing shows mirrored swings, swap the clip constants in MumakilConfig — not this sign.)
    /// </summary>
    BTBlackboardValue<float> TargetBearing { get; set; }
}
