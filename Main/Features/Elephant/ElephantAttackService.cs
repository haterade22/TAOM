using System;

namespace TAOM.Features.Elephant;

/// <summary>
/// Pure war-elephant decision logic, with no TaleWorlds dependencies so it is fully unit-tested. The damage
/// formula and the facing gate are 1-for-1 ADOD (decompiled 2026-06-05); the cooldown model (2026-06-10)
/// is TAOM's — it replaced ADOD's per-tick probability roll so the BT can sequence trample → side attacks
/// deterministically. The behavior-tree nodes supply the engine values (facing dot, current time, blocking).
/// </summary>
public class ElephantAttackService : IElephantAttackService
{
    public bool IsElephantMonster(string? monsterId) => monsterId == ElephantConfig.ElephantMonsterId;

    public bool ShouldEngage(float facingDot, bool alreadyAttacking)
        => !alreadyAttacking && facingDot > ElephantConfig.TrampleFacingDot;

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public int ComputeInflictedDamage(bool targetBlocking)
    {
        // ADOD: num = round(10 * (blocking ? 0.25 : 1)); inflicted = num * 2.
        int baseDamage = (int)Math.Round(ElephantConfig.TrampleBaseDamage * (targetBlocking ? 0.25f : 1f));
        return baseDamage * 2;
    }
}
