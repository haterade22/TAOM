using TAOM.Features.ElephantLike;

namespace TAOM.Features.Animalia;

/// <summary>
/// The Animalia elk's binding of the shared <see cref="ElephantLikeAttackService"/>: pure decision logic, no TaleWorlds
/// dependencies. One fixed blow, so both ends of the Trample band are the damage; the SideAttack band mirrors it (no
/// side attack is wired, as on the great elk).
/// </summary>
public class AnimaliaElkAttackService : ElephantLikeAttackService, IAnimaliaElkAttackService
{
    public AnimaliaElkAttackService() : base(
        AnimaliaConfig.ElkMonsterId,
        AnimaliaConfig.AttackFacingDot,
        AnimaliaConfig.ElkAttackDamage,
        AnimaliaConfig.ElkAttackDamage,
        AnimaliaConfig.ElkAttackDamage,
        AnimaliaConfig.ElkAttackDamage,
        AnimaliaConfig.BlockedDamageMultiplier)
    {
    }
}
