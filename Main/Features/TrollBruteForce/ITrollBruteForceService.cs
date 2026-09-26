namespace TAOM.Features.TrollBruteForce;

/// <summary>What one enemy in the Brute Force ring takes.</summary>
public readonly record struct BruteForceBlow(int Damage, bool KnockDown);

/// <summary>
/// Pure decisions for the trolls' Brute Force smash (#649). Floats in, no TaleWorlds types (ADR-007);
/// every float is an engine value, so a NaN or infinity fails closed. A bad body scale reads as 1.
/// </summary>
public interface ITrollBruteForceService
{
    /// <summary>True only for a battle troll's Monster id, a key of <see cref="TrollBruteForceConfig.ActionSetsByMonster"/>.</summary>
    bool IsBruteForceTroll(string? monsterId);

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

    /// <summary>
    /// The troll's body size for every distance below: <paramref name="agentScale"/> times its Monster's eye height
    /// over <see cref="TrollBruteForceConfig.ReferenceEyeHeight"/>. A non-finite or non-positive eye height keeps the
    /// agent scale; a bad agent scale passes through and the distance rules read it as 1.
    /// </summary>
    float BodySize(float agentScale, float standingEyeHeight);

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

    /// <summary>
    /// A troll's width in formation: its Monster's <see cref="TrollBruteForceConfig.ShoulderWidthByMonster"/> times
    /// <paramref name="agentScale"/>. Zero for any other Monster or a non-finite or non-positive scale.
    /// </summary>
    float TrollWidth(string? monsterId, float agentScale);

    /// <summary>
    /// The unit width a formation is spaced for once trolls make up <see cref="TrollBruteForceConfig.FormationShare"/>
    /// of it: the widest troll's <see cref="TrollWidth"/>, capped at
    /// <see cref="TrollBruteForceConfig.MaxFormationUnitWidth"/>. Null keeps vanilla: too few trolls, none wider than
    /// <paramref name="vanillaDiameter"/> (the engine's human width), or a non-finite input. Vanilla's rule for horses,
    /// <c>Formation.CalculateHasSignificantNumberOfMounted</c>.
    /// </summary>
    float? FormationUnitDiameter(float vanillaDiameter, int unitCount, int trollCount, float widestTroll);
}
