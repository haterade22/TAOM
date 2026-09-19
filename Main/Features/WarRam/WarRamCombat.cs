using TAOM.Features.ElephantLike.BehaviorTreeElements;

namespace TAOM.Features.WarRam;

/// <summary>
/// The war ram's boundary tuning for the shared elephant-like BT nodes: scan ranges + blow magnitude
/// from <see cref="WarRamConfig"/>, the four attack-clip caches (all the bespoke head-butt act_war_ram_butt,
/// see <see cref="WarRamConfig"/>'s remarks on why all four slots are the same action, resolved
/// eagerly at first touch), the single-target switch, and the
/// lazy resolver for the ram's registered <see cref="IWarRamAttackService"/>.
/// </summary>
internal static class WarRamCombat
{
    internal static readonly ElephantLikeCombatProfile Profile = new(
        WarRamConfig.AttackTriggerRange,
        WarRamConfig.AttackRadius,
        WarRamConfig.AttackBlowMagnitude,
        WarRamConfig.AttackActionName,
        WarRamConfig.AttackAltActionName,
        WarRamConfig.SideSlotLeftActionName,
        WarRamConfig.SideSlotRightActionName,
        () => IoC.Resolve<IWarRamAttackService>(),
        singleTarget: WarRamConfig.AttackSingleTarget);
}
