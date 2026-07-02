using TAOM.Features.ElephantLike.BehaviorTreeElements;

namespace TAOM.Features.Elephant;

/// <summary>
/// The war-elephant's boundary tuning for the shared elephant-like BT nodes: scan ranges + blow magnitude from
/// <see cref="ElephantConfig"/>, the four attack-clip caches (resolved eagerly at first touch — the
/// resolve-once semantics the old per-feature ElephantAttackActions had), and the lazy resolver for the
/// elephant's registered <see cref="IElephantAttackService"/>.
/// </summary>
internal static class ElephantCombat
{
    internal static readonly ElephantLikeCombatProfile Profile = new(
        ElephantConfig.TrampleTriggerRange,
        ElephantConfig.TrampleRadius,
        ElephantConfig.TrampleBlowMagnitude,
        ElephantConfig.TrampleActionName,
        ElephantConfig.TrampleAltActionName,
        ElephantConfig.SideAttackLeftActionName,
        ElephantConfig.SideAttackRightActionName,
        () => IoC.Resolve<IElephantAttackService>());
}
