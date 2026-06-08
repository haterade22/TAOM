using System;

namespace TAOM.Features.Elephant;

/// <summary>
/// Pure war-elephant decision logic — a faithful 1-for-1 of ADOD_Beasts'
/// <c>ADODBeastsElephantAgentComponent.OnTickAsAI</c> gates + damage formula (decompiled 2026-06-05),
/// with no TaleWorlds dependencies so it is fully unit-tested. <see cref="ElephantMissionBehavior"/> supplies
/// the engine values (distance, facing dot, random roll, blocking state) and applies the result.
/// </summary>
public class ElephantAttackService : IElephantAttackService
{
    public bool IsElephantMonster(string? monsterId) => monsterId == ElephantConfig.ElephantMonsterId;

    public bool ShouldAiTrample(float facingDot, float randomRoll, bool alreadyAttacking)
    {
        return !alreadyAttacking
            && facingDot > ElephantConfig.TrampleFacingDot
            && randomRoll < ElephantConfig.TrampleChancePerTick;
    }

    public int ComputeInflictedDamage(bool targetBlocking)
    {
        // ADOD: num = round(10 * (blocking ? 0.25 : 1)); inflicted = num * 2.
        int baseDamage = (int)Math.Round(ElephantConfig.TrampleBaseDamage * (targetBlocking ? 0.25f : 1f));
        return baseDamage * 2;
    }
}
