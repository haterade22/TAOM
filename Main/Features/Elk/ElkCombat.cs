using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elk;

/// <summary>
/// The great elk's boundary tuning for the shared elephant-like BT nodes: scan ranges and blow magnitude from
/// <see cref="ElkConfig"/>, the four attack-clip caches (all the antler charge, resolved eagerly at first touch),
/// the single-target switch, the Blunt damage type, the rider's charge bonus, and the lazy resolver for the elk's
/// registered <see cref="IElkAttackService"/>.
/// </summary>
internal static class ElkCombat
{
    private static ICareerAgentStatService? _careerStats;

    internal static readonly ElephantLikeCombatProfile Profile = new(
        ElkConfig.AttackTriggerRange,
        ElkConfig.AttackRadius,
        ElkConfig.AttackBlowMagnitude,
        ElkConfig.AttackActionName,
        ElkConfig.AttackAltActionName,
        ElkConfig.SideSlotLeftActionName,
        ElkConfig.SideSlotRightActionName,
        () => IoC.Resolve<IElkAttackService>(),
        singleTarget: ElkConfig.AttackSingleTarget,
        damageType: ElkConfig.AttackDamageType,
        riderMultiplier: RiderChargeMultiplier,
        reachScalesWithBody: ElkConfig.ReachScalesWithBody);

    /// <summary>
    /// The rider's career charge bonus, read when the antler charge fires (Mike, 2026-09-23: "scale the antler blows
    /// too"). It is the product that scales the elk's own MountChargeDamage, so the body charge and the antler blow
    /// move together: the rider hero's MountChargeDamage passive, and the Antler Crash charge bonus from the rider's own
    /// buff or the ally buff under the rider's index. 1 for a rider with none of them, which is every rider in Custom
    /// Battle: no rider there is a campaign hero, and the career mission behavior attaches only in a campaign
    /// (SubModule). Boundary code: the ids are extracted here, the product is
    /// <see cref="ICareerAgentStatService.MountChargeMultiplier"/>'s.
    /// </summary>
    private static float RiderChargeMultiplier(Agent rider)
    {
        _careerStats ??= IoC.Resolve<ICareerAgentStatService>();
        string? heroId = rider.IsHero ? (rider.Character as CharacterObject)?.HeroObject?.StringId : null;
        return _careerStats.MountChargeMultiplier(heroId, rider.Index);
    }
}
