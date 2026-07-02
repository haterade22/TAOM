using System;

namespace TAOM.Features.ElephantLike;

/// <summary>
/// Shared elephant-like decision logic, with no TaleWorlds dependencies so it is fully unit-tested. The damage
/// formula and the facing gate are 1-for-1 with the upstream pack (decompiled 2026-06-05); the cooldown model
/// (2026-06-10) is TAOM's — it replaced the upstream pack's per-tick probability roll so the BT can sequence
/// trample → side attacks deterministically. Subclasses bind one creature's tuning constants
/// (<c>ElephantConfig</c> / <c>MumakilConfig</c>); the behavior-tree nodes supply the engine values
/// (facing dot, current time, blocking).
/// </summary>
public abstract class ElephantLikeAttackService : IElephantLikeAttackService
{
    private readonly string _monsterId;
    private readonly float _facingDot;
    private readonly int _trampleMinDamage;
    private readonly int _trampleMaxDamage;
    private readonly int _tuskMinDamage;
    private readonly int _tuskMaxDamage;
    private readonly float _blockedDamageMultiplier;

    protected ElephantLikeAttackService(
        string monsterId,
        float facingDot,
        int trampleMinDamage,
        int trampleMaxDamage,
        int tuskMinDamage,
        int tuskMaxDamage,
        float blockedDamageMultiplier)
    {
        _monsterId = monsterId;
        _facingDot = facingDot;
        _trampleMinDamage = trampleMinDamage;
        _trampleMaxDamage = trampleMaxDamage;
        _tuskMinDamage = tuskMinDamage;
        _tuskMaxDamage = tuskMaxDamage;
        _blockedDamageMultiplier = blockedDamageMultiplier;
    }

    public bool IsCreatureMonster(string? monsterId) => monsterId == _monsterId;

    public bool ShouldEngage(float facingDot, bool alreadyAttacking)
        => !alreadyAttacking && facingDot > _facingDot;

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public int ComputeInflictedDamage(ElephantLikeAttackKind kind, bool targetBlocking, float roll)
    {
        int min = kind == ElephantLikeAttackKind.Trample ? _trampleMinDamage : _tuskMinDamage;
        int max = kind == ElephantLikeAttackKind.Trample ? _trampleMaxDamage : _tuskMaxDamage;

        // Clamp the roll to [0,1]; an explicit NaN check is required because NaN comparisons are always false
        // (no float.IsFinite on .NET Framework 4.7.2). A NaN/below-range roll collapses to the band minimum.
        float r = float.IsNaN(roll) || roll < 0f ? 0f : (roll > 1f ? 1f : roll);

        int rolled = min + (int)Math.Round(r * (max - min));
        float mult = targetBlocking ? _blockedDamageMultiplier : 1f;
        return (int)Math.Round(rolled * mult);
    }
}
