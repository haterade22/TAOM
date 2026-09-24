using TAOM.Features.ElephantLike;

namespace TAOM.Features.Animalia;

/// <summary>The Animalia moose's binding of the shared <see cref="ElephantLikeAttackService"/>.</summary>
public class AnimaliaMooseAttackService : ElephantLikeAttackService, IAnimaliaMooseAttackService
{
    public AnimaliaMooseAttackService() : base(
        AnimaliaConfig.MooseMonsterId,
        AnimaliaConfig.AttackFacingDot,
        AnimaliaConfig.MooseAttackDamage,
        AnimaliaConfig.MooseAttackDamage,
        AnimaliaConfig.MooseAttackDamage,
        AnimaliaConfig.MooseAttackDamage,
        AnimaliaConfig.BlockedDamageMultiplier)
    {
    }
}
