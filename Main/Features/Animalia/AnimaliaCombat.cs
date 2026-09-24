using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Animalia;

/// <summary>
/// The Animalia elk and moose's boundary tuning for the shared elephant-like BT nodes: one static profile each (so
/// <c>ActionIndexCache.Create</c> keeps its resolve-once semantics), holding the scan ranges and blow magnitude from
/// <see cref="AnimaliaConfig"/>, the attack clip in all four slots (IsAttack ORs across them, so any other action
/// there would widen "mid-attack" to an unrelated engine-driven action; the great elk's reason), the single-target
/// switch, the Blunt damage type, the rider's charge bonus, and the lazy resolver for each animal's service.
/// </summary>
internal static class AnimaliaCombat
{
    private static ICareerAgentStatService? _careerStats;

    internal static readonly ElephantLikeCombatProfile ElkProfile = new(
        AnimaliaConfig.AttackTriggerRange,
        AnimaliaConfig.AttackRadius,
        AnimaliaConfig.ElkBlowMagnitude,
        AnimaliaConfig.ElkAttackActionName,
        AnimaliaConfig.ElkAttackActionName,
        AnimaliaConfig.ElkAttackActionName,
        AnimaliaConfig.ElkAttackActionName,
        () => IoC.Resolve<IAnimaliaElkAttackService>(),
        singleTarget: AnimaliaConfig.AttackSingleTarget,
        damageType: AnimaliaConfig.AttackDamageType,
        riderMultiplier: RiderChargeMultiplier,
        reachScalesWithBody: AnimaliaConfig.ReachScalesWithBody);

    internal static readonly ElephantLikeCombatProfile MooseProfile = new(
        AnimaliaConfig.AttackTriggerRange,
        AnimaliaConfig.AttackRadius,
        AnimaliaConfig.MooseBlowMagnitude,
        AnimaliaConfig.MooseAttackActionName,
        AnimaliaConfig.MooseAttackActionName,
        AnimaliaConfig.MooseAttackActionName,
        AnimaliaConfig.MooseAttackActionName,
        () => IoC.Resolve<IAnimaliaMooseAttackService>(),
        singleTarget: AnimaliaConfig.AttackSingleTarget,
        damageType: AnimaliaConfig.AttackDamageType,
        riderMultiplier: RiderChargeMultiplier,
        reachScalesWithBody: AnimaliaConfig.ReachScalesWithBody);

    /// <summary>
    /// The rider's career charge bonus, the same product that scales the great elk's antler blow
    /// (<see cref="ICareerAgentStatService.MountChargeMultiplier"/>): 1 for a rider with none, which is every rider in
    /// Custom Battle (no campaign hero there). Boundary code: the ids are extracted here.
    /// </summary>
    private static float RiderChargeMultiplier(Agent rider)
    {
        _careerStats ??= IoC.Resolve<ICareerAgentStatService>();
        string? heroId = rider.IsHero ? (rider.Character as CharacterObject)?.HeroObject?.StringId : null;
        return _careerStats.MountChargeMultiplier(heroId, rider.Index);
    }
}
