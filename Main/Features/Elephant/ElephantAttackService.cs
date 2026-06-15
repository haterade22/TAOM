using System;
using TAOM.Features.Elephant.BehaviorTreeElements;

namespace TAOM.Features.Elephant;

/// <summary>
/// Pure war-elephant decision logic, with no TaleWorlds dependencies so it is fully unit-tested. The damage
/// formula and the facing gate are 1-for-1 with the upstream pack (decompiled 2026-06-05); the cooldown model (2026-06-10)
/// is TAOM's — it replaced the upstream pack's per-tick probability roll so the BT can sequence trample → side attacks
/// deterministically. The behavior-tree nodes supply the engine values (facing dot, current time, blocking).
/// </summary>
public class ElephantAttackService : IElephantAttackService
{
    public bool IsElephantMonster(string? monsterId) => monsterId == ElephantConfig.ElephantMonsterId;

    public bool ShouldEngage(float facingDot, bool alreadyAttacking)
        => !alreadyAttacking && facingDot > ElephantConfig.TrampleFacingDot;

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public int ComputeInflictedDamage(ElephantAttackKind kind, bool targetBlocking, float roll)
    {
        int min = kind == ElephantAttackKind.Trample ? ElephantConfig.TrampleMinDamage : ElephantConfig.TuskMinDamage;
        int max = kind == ElephantAttackKind.Trample ? ElephantConfig.TrampleMaxDamage : ElephantConfig.TuskMaxDamage;

        // Clamp the roll to [0,1]; an explicit NaN check is required because NaN comparisons are always false
        // (no float.IsFinite on .NET Framework 4.7.2). A NaN/below-range roll collapses to the band minimum.
        float r = float.IsNaN(roll) || roll < 0f ? 0f : (roll > 1f ? 1f : roll);

        int rolled = min + (int)Math.Round(r * (max - min));
        float mult = targetBlocking ? ElephantConfig.BlockedDamageMultiplier : 1f;
        return (int)Math.Round(rolled * mult);
    }
}
