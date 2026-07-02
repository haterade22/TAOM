using TAOM.Features.ElephantLike.BehaviorTreeElements;

namespace TAOM.Features.Mumakil;

/// <summary>
/// The Mûmakil's boundary tuning for the shared elephant-like BT nodes: scan ranges + blow magnitude from
/// <see cref="MumakilConfig"/> (3×-scaled trigger range/radius), the four attack-clip caches (the ELEPHANT's
/// clips — the Mûmakil shares the as_elephant action set; resolved eagerly at first touch), and the lazy
/// resolver for the Mûmakil's registered <see cref="IMumakilAttackService"/>.
/// </summary>
internal static class MumakilCombat
{
    internal static readonly ElephantLikeCombatProfile Profile = new(
        MumakilConfig.TrampleTriggerRange,
        MumakilConfig.TrampleRadius,
        MumakilConfig.TrampleBlowMagnitude,
        MumakilConfig.TrampleActionName,
        MumakilConfig.TrampleAltActionName,
        MumakilConfig.SideAttackLeftActionName,
        MumakilConfig.SideAttackRightActionName,
        () => IoC.Resolve<IMumakilAttackService>());
}
