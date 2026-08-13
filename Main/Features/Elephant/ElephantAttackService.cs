using TAOM.Features.ElephantLike;

namespace TAOM.Features.Elephant;

/// <summary>
/// War-elephant binding of the shared <see cref="ElephantLikeAttackService"/> — pure decision logic (no
/// TaleWorlds dependencies, fully unit-tested) bound to <see cref="ElephantConfig"/>'s tuning. The damage
/// formula and the facing gate are 1-for-1 with ADOD_Beasts (decompiled 2026-06-05); the cooldown model
/// (2026-06-10) is TAOM's — it replaced ADOD_Beasts's per-tick probability roll so the BT can sequence
/// trample → side attacks deterministically.
/// </summary>
public class ElephantAttackService : ElephantLikeAttackService, IElephantAttackService
{
    public ElephantAttackService() : base(
        ElephantConfig.ElephantMonsterId,
        ElephantConfig.TrampleFacingDot,
        ElephantConfig.TrampleMinDamage,
        ElephantConfig.TrampleMaxDamage,
        ElephantConfig.TuskMinDamage,
        ElephantConfig.TuskMaxDamage,
        ElephantConfig.BlockedDamageMultiplier)
    {
    }
}
