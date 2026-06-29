using System;
using TAOM.Features.Mumakil.BehaviorTreeElements;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Pure Mûmakil decision logic, with no TaleWorlds dependencies so it is fully unit-tested. 1-for-1 with
/// <see cref="TAOM.Features.Elephant.ElephantAttackService"/> (the Mûmakil is a scaled-up elephant): the damage
/// band + facing gate + deterministic cooldown model are identical. The behavior-tree nodes supply the engine
/// values (facing dot, current time, blocking).
/// </summary>
public class MumakilAttackService : IMumakilAttackService
{
    public bool IsMumakilMonster(string? monsterId) => monsterId == MumakilConfig.MumakilMonsterId;

    public bool ShouldEngage(float facingDot, bool alreadyAttacking)
        => !alreadyAttacking && facingDot > MumakilConfig.TrampleFacingDot;

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public int ComputeInflictedDamage(MumakilAttackKind kind, bool targetBlocking, float roll)
    {
        int min = kind == MumakilAttackKind.Trample ? MumakilConfig.TrampleMinDamage : MumakilConfig.TuskMinDamage;
        int max = kind == MumakilAttackKind.Trample ? MumakilConfig.TrampleMaxDamage : MumakilConfig.TuskMaxDamage;

        // Clamp the roll to [0,1]; an explicit NaN check is required because NaN comparisons are always false
        // (no float.IsFinite on .NET Framework 4.7.2). A NaN/below-range roll collapses to the band minimum.
        float r = float.IsNaN(roll) || roll < 0f ? 0f : (roll > 1f ? 1f : roll);

        int rolled = min + (int)Math.Round(r * (max - min));
        float mult = targetBlocking ? MumakilConfig.BlockedDamageMultiplier : 1f;
        return (int)Math.Round(rolled * mult);
    }
}
