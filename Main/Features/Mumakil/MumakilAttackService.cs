using TAOM.Features.ElephantLike;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Mûmakil binding of the shared <see cref="ElephantLikeAttackService"/> — pure decision logic (no TaleWorlds
/// dependencies, fully unit-tested) bound to <see cref="MumakilConfig"/>'s tuning. 1-for-1 with the war
/// elephant's binding (the Mûmakil is a scaled-up elephant): the damage band + facing gate + deterministic
/// cooldown model are identical; only the scan ranges differ (3× body scale), and those live in
/// <see cref="MumakilCombat.Profile"/>, not here.
/// </summary>
public class MumakilAttackService : ElephantLikeAttackService, IMumakilAttackService
{
    public MumakilAttackService() : base(
        MumakilConfig.MumakilMonsterId,
        MumakilConfig.TrampleFacingDot,
        MumakilConfig.TrampleMinDamage,
        MumakilConfig.TrampleMaxDamage,
        MumakilConfig.TuskMinDamage,
        MumakilConfig.TuskMaxDamage,
        MumakilConfig.BlockedDamageMultiplier)
    {
    }
}
