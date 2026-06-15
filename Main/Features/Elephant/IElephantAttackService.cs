using System;
using TAOM.Features.Elephant.BehaviorTreeElements;

namespace TAOM.Features.Elephant;

/// <summary>
/// Pure decision logic for the war-elephant — extracted from the engine so it is unit-testable (ADR-002/007).
/// The engine-coupled work (radial agent scan, action channel, damage application) lives in the behavior-tree
/// nodes under <c>BehaviorTreeElements/</c>. The 2026-06-10 cooldown rework replaced the upstream pack's per-tick probability
/// roll with deterministic cooldowns: the BT gates each attack on <see cref="IsOffCooldown"/> and the shared
/// facing/anim gate <see cref="ShouldEngage"/>.
/// </summary>
public interface IElephantAttackService
{
    /// <summary>True when the given Monster StringId is the war elephant (drives mount identification + lock).</summary>
    bool IsElephantMonster(string? monsterId);

    /// <summary>
    /// Shared engage gate for every elephant attack: the elephant must face its best in-range enemy
    /// (strict <c>&gt; TrampleFacingDot</c>, upstream-pack parity) and must not already be mid-attack-animation.
    /// The BT scan passes <paramref name="facingDot"/> = -1 when no live enemy is within trigger range.
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
    /// scales by <see cref="ElephantConfig.BlockedDamageMultiplier"/> when <paramref name="targetBlocking"/>.
    /// Trample 50-100, side-swing (tusk) 50-75 before block scaling.
    /// </summary>
    int ComputeInflictedDamage(ElephantAttackKind kind, bool targetBlocking, float roll);
}
