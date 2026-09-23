using TAOM.Features.ElephantLike;

namespace TAOM.Features.Elk;

/// <summary>
/// Great elk binding of the shared <see cref="ElephantLikeAttackService"/>: pure decision logic (no TaleWorlds
/// dependencies, fully unit-tested) bound to <see cref="ElkConfig"/>'s tuning. The antler charge is one fixed blow, so
/// both ends of the Trample band are <see cref="ElkConfig.AttackDamage"/>; the SideAttack band mirrors it (no side
/// attack is wired).
/// </summary>
public class ElkAttackService : ElephantLikeAttackService, IElkAttackService
{
    public ElkAttackService() : base(
        ElkConfig.ElkMonsterId,
        ElkConfig.AttackFacingDot,
        ElkConfig.AttackDamage,
        ElkConfig.AttackDamage,
        ElkConfig.AttackDamage,
        ElkConfig.AttackDamage,
        ElkConfig.BlockedDamageMultiplier)
    {
    }
}
