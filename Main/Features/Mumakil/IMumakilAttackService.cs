using System;
using TAOM.Features.Mumakil.BehaviorTreeElements;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Pure decision logic for the Mûmakil — extracted from the engine so it is unit-testable (ADR-002/007).
/// The engine-coupled work (radial agent scan, action channel, damage application) lives in the behavior-tree
/// nodes under <c>BehaviorTreeElements/</c>. 1-for-1 with <see cref="TAOM.Features.Elephant.IElephantAttackService"/>;
/// the Mûmakil is a scaled-up elephant so it shares the attack model (deterministic cooldowns, per-kind damage band).
/// </summary>
public interface IMumakilAttackService
{
    /// <summary>True when the given Monster StringId is the Mûmakil (drives mount identification + lock).</summary>
    bool IsMumakilMonster(string? monsterId);

    /// <summary>
    /// Shared engage gate for every Mûmakil attack: it must face its best in-range enemy
    /// (strict <c>&gt; TrampleFacingDot</c>) and must not already be mid-attack-animation. The BT scan passes
    /// <paramref name="facingDot"/> = -1 when no live enemy is within trigger range.
    /// </summary>
    bool ShouldEngage(float facingDot, bool alreadyAttacking);

    /// <summary>
    /// Deterministic cooldown check: true when the attack has never fired or at least
    /// <paramref name="cooldownSeconds"/> have elapsed (inclusive). A future <paramref name="lastFired"/>
    /// (clock skew / bad stamp) reads as ON cooldown.
    /// </summary>
    bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds);

    /// <summary>
    /// Per-hit damage for an attack <paramref name="kind"/>: rolls a value in the kind's [min,max] band using
    /// <paramref name="roll"/> (a [0,1] float from <c>MBRandom.RandomFloat</c>, clamped + NaN-guarded here), then
    /// scales by <see cref="MumakilConfig.BlockedDamageMultiplier"/> when <paramref name="targetBlocking"/>.
    /// Trample 50-100, side-swing (tusk) 50-75 before block scaling.
    /// </summary>
    int ComputeInflictedDamage(MumakilAttackKind kind, bool targetBlocking, float roll);
}
