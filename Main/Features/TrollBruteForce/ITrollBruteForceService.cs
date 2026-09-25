namespace TAOM.Features.TrollBruteForce;

/// <summary>What one enemy in the Brute Force ring takes.</summary>
public readonly record struct BruteForceBlow(int Damage, bool KnockDown);

/// <summary>
/// Pure decisions for the cave troll's Brute Force smash (#649). Floats in, no TaleWorlds types (ADR-007);
/// every float is an engine value, so a NaN or infinity fails closed. A bad body scale reads as 1.
/// </summary>
public interface ITrollBruteForceService
{
    /// <summary>True only for the battle troll's Monster id, <see cref="TrollBruteForceConfig.CaveTrollMonsterId"/>.</summary>
    bool IsCaveTroll(string? monsterId);

    /// <summary>
    /// True when the smash never fired or <see cref="TrollBruteForceConfig.CooldownSeconds"/> of mission time have
    /// passed (inclusive). A stamp in the future reads as on cooldown.
    /// </summary>
    bool IsOffCooldown(float? lastFired, float now);

    /// <summary>
    /// True when the troll is free (not <paramref name="busy"/>) and its nearest enemy is within the scaled trigger
    /// range and strictly in front. The scan passes a facing of -1 when it found no enemy.
    /// </summary>
    bool ShouldEngage(float enemyDistance, float facingDot, float bodyScale, bool busy);

    /// <summary>The ring's centre on the ground plane, <see cref="TrollBruteForceConfig.ImpactForward"/> (scaled) along the look direction.</summary>
    bool TryGetImpactCentre(float x, float y, float lookX, float lookY, float bodyScale, out float centreX, out float centreY);

    /// <summary>True once the smash clip's progress has reached <see cref="TrollBruteForceConfig.ImpactFraction"/>.</summary>
    bool HasReachedImpact(float progress);

    /// <summary>
    /// The blow for an enemy <paramref name="distance"/> metres from the ring's centre: the engine's area falloff
    /// on <see cref="TrollBruteForceConfig.CentreDamage"/>, a shield-block scaled and not knocked down. Null outside
    /// the ring.
    /// </summary>
    BruteForceBlow? DecideRingBlow(float distance, float bodyScale, bool shieldBlocked);

    /// <summary>The ring's scaled outer radius, for the agent query.</summary>
    float OuterRadius(float bodyScale);
}
