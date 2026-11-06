using System;
using TAOM.Core.Validation;
using static TAOM.Features.DwarfAttacks.DwarfAttacksConfig;

namespace TAOM.Features.DwarfAttacks;

public enum DwarfAttack
{
    HeadButt,
    DropKick,
}

/// <summary>What the victim of one attack takes.</summary>
public readonly record struct DwarfAttackBlow(int Damage, bool KnockDown);

/// <summary>
/// Pure decisions for the dwarves' head butt and drop kick (#1087). Floats in, no TaleWorlds types (ADR-007). Every
/// float is an engine value, so a NaN or infinity fails closed.
/// </summary>
public sealed class DwarfAttackService
{
    /// <summary>True only for the dwarf battle Monster.</summary>
    public bool IsDwarf(string? monsterId) => string.Equals(monsterId, DwarfMonsterId, StringComparison.Ordinal);

    /// <summary>True when the attack never fired or its own cooldown has passed (inclusive). A stamp in the future reads as on cooldown.</summary>
    public bool IsOffCooldown(DwarfAttack attack, float? lastFired, float now)
    {
        if (lastFired is not float last) return true;
        if (!FiniteFloatValidator.IsFinite(last) || !FiniteFloatValidator.IsFinite(now)) return false;
        return now - last >= Cooldown(attack);
    }

    /// <summary>
    /// True when the attack may start: its action resolved (a missing Armory action resolves to act_none, and playing
    /// act_none clears the dwarf's channel) and it is off cooldown.
    /// </summary>
    public bool IsReady(DwarfAttack attack, bool actionResolved, float? lastFired, float now) =>
        actionResolved && IsOffCooldown(attack, lastFired, now);

    /// <summary>
    /// True when the dwarf may attack at all: not the player's own character (also after TAOM hands him to the AI), not
    /// mounted (neither clip has a rider version), and not on a machine such as a ballista or a ladder.
    /// </summary>
    public bool MayAttack(bool isPlayersOwn, bool mounted, bool onMachine) => !isPlayersOwn && !mounted && !onMachine;

    /// <summary>The idle scan period for the dwarf built <paramref name="ordinal"/>th, spread over a window.</summary>
    public TimeSpan IdleScanPeriod(int ordinal) =>
        TimeSpan.FromMilliseconds(IdleScanMinMilliseconds + ordinal % IdleScanSpreadMilliseconds);

    /// <summary>True when an enemy is on the dwarf's level, within <see cref="DwarfAttacksConfig.MaxHeightDifference"/>.</summary>
    public bool IsWithinHeight(float heightDifference) =>
        FiniteFloatValidator.IsFinite(heightDifference) && Math.Abs(heightDifference) <= MaxHeightDifference;

    /// <summary>
    /// True when the victim's raised shield covers the dwarf: <paramref name="victimFacingDot"/> is the cosine between the
    /// victim's look direction and the direction to the dwarf.
    /// </summary>
    public bool ShieldBlocks(bool shieldRaised, float victimFacingDot) =>
        shieldRaised && FiniteFloatValidator.IsFinite(victimFacingDot) && victimFacingDot > ShieldFacingDot;

    /// <summary>
    /// The attack for the dwarf's nearest enemy: the head butt in close, the drop kick in its band, nothing between
    /// the bands, beyond them, behind the dwarf or while the matching attack is on cooldown.
    /// </summary>
    public DwarfAttack? ChooseAttack(float nearestEnemyDistance, float facingDot, bool headButtReady, bool dropKickReady)
    {
        if (!FiniteFloatValidator.IsFinite(nearestEnemyDistance) || !FiniteFloatValidator.IsFinite(facingDot)) return null;
        if (!(nearestEnemyDistance >= 0f) || !(facingDot > FacingDot)) return null;
        if (nearestEnemyDistance <= HeadButtMaxRange) return headButtReady ? DwarfAttack.HeadButt : null;
        if (nearestEnemyDistance >= DropKickMinRange && nearestEnemyDistance <= DropKickMaxRange)
            return dropKickReady ? DwarfAttack.DropKick : null;
        return null;
    }

    /// <summary>True once the clip's progress has reached the attack's impact fraction.</summary>
    public bool HasReachedImpact(DwarfAttack attack, float progress) =>
        FiniteFloatValidator.IsFinite(progress) && progress >= ImpactFraction(attack);

    /// <summary>The radius the impact searches for a victim around the dwarf.</summary>
    public float Reach(DwarfAttack attack) => attack == DwarfAttack.HeadButt ? HeadButtReach : DropKickReach;

    /// <summary>True for an enemy within the attack's reach and in front of the dwarf.</summary>
    public bool CanHit(DwarfAttack attack, float distance, float facingDot) =>
        FiniteFloatValidator.IsFinite(distance) && FiniteFloatValidator.IsFinite(facingDot)
        && distance >= 0f && distance <= Reach(attack) && facingDot > HitFacingDot;

    /// <summary>
    /// The victim's blow: the attack's damage times <paramref name="damagePercent"/> (capped at
    /// <see cref="DwarfAttacksConfig.MaxDamagePercent"/>), scaled for a shield block. Only an unblocked drop kick
    /// floors the victim. Null when the damage rounds to nothing, so the slider at 0 turns the hit off.
    /// </summary>
    public DwarfAttackBlow? DecideBlow(DwarfAttack attack, bool shieldBlocked, int damagePercent)
    {
        int percent = Math.Min(damagePercent, MaxDamagePercent);
        if (percent <= 0) return null;

        int baseDamage = attack == DwarfAttack.HeadButt ? HeadButtDamage : DropKickDamage;
        int damage = (int)Math.Round(baseDamage * (percent / 100.0) * (shieldBlocked ? ShieldBlockedMultiplier : 1.0));
        if (damage <= 0) return null;
        return new DwarfAttackBlow(damage, KnockDown: attack == DwarfAttack.DropKick && !shieldBlocked);
    }

    private static float Cooldown(DwarfAttack attack) =>
        attack == DwarfAttack.HeadButt ? HeadButtCooldownSeconds : DropKickCooldownSeconds;

    private static float ImpactFraction(DwarfAttack attack) =>
        attack == DwarfAttack.HeadButt ? HeadButtImpactFraction : DropKickImpactFraction;
}
