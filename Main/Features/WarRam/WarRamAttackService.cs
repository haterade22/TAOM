using TAOM.Features.ElephantLike;

namespace TAOM.Features.WarRam;

/// <summary>
/// War ram binding of the shared <see cref="ElephantLikeAttackService"/>, pure decision logic (no
/// TaleWorlds dependencies, fully unit-tested) bound to <see cref="WarRamConfig"/>'s tuning. Only the
/// Trample-kind band (the ram's single kick attack) is ever exercised in-game:
/// <see cref="WarRamBehaviorTree"/> wires no side-attack branch, so
/// <c>ComputeInflictedDamage(SideAttack, ...)</c> is reachable only from a test, never from the tree.
/// Its band mirrors the kick band 1-for-1 rather than a distinct value, so an accidental call never
/// produces a nonsensical result.
/// </summary>
public class WarRamAttackService : ElephantLikeAttackService, IWarRamAttackService
{
    public WarRamAttackService() : base(
        WarRamConfig.WarRamMonsterId,
        WarRamConfig.AttackFacingDot,
        WarRamConfig.AttackMinDamage,
        WarRamConfig.AttackMaxDamage,
        WarRamConfig.AttackMinDamage,
        WarRamConfig.AttackMaxDamage,
        WarRamConfig.BlockedDamageMultiplier)
    {
    }
}
