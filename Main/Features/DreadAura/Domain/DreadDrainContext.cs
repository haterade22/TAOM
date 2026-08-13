namespace TAOM.Features.DreadAura.Domain;

/// <summary>
/// Everything the pure drain calculation needs about one (source, target) pair. Built at the
/// boundary by the MissionLogic so no engine type crosses into the service (ADR-007).
/// </summary>
public readonly struct DreadDrainContext
{
    /// <summary>The configured morale-per-second rate already divided by
    /// vanilla's per-recipient resistance, i.e. the result of
    /// <c>BattleMoraleModel.CalculateMoraleChangeToCharacter(target, moralePerSecond)</c>.
    ///
    /// Taking the divisor from the registered model rather than reimplementing
    /// <c>(IsHero ? 1.5 : 1) * (0.5 * tier + 1)</c> keeps dread scaling at exact parity with how
    /// the engine scales kill-morale, and it means the model is CALLED, never overridden. An
    /// override would be wrong: the caller applies the sign
    /// (<c>AgentMoraleInteractionLogic.cs:110</c> negates, <c>:115</c> does not), so shaping that
    /// method would shrink the morale a resistant race GAINS from kills just as much as the dread
    /// it takes.</summary>
    public float ResistScaledRate { get; }

    /// <summary>Squared planar distance from source to target, in m². Squared to avoid a sqrt on
    /// the reject path; the falloff branch takes the root only for targets already in range.</summary>
    public float DistanceSq { get; }

    public float Radius { get; }

    public float InnerRadius { get; }

    /// <summary>The target's FaceGen race id, or null when the agent has no character. Resolved
    /// against the resist table by the service, which validates the id before any name lookup.</summary>
    public int? TargetRaceId { get; }

    /// <summary>The target's current morale. -1f is the engine's "no CommonAIComponent" sentinel
    /// and must fail the floor gate rather than pass it as "already drained".</summary>
    public float CurrentMorale { get; }

    /// <summary>Seconds since this SOURCE last pulsed, not since the last frame. Sources are
    /// round-robined across frames, so integrating against per-source elapsed time keeps the drain
    /// rate-exact no matter how many wraiths share the schedule.</summary>
    public float ElapsedSeconds { get; }

    public DreadDrainContext(
        float resistScaledRate,
        float distanceSq,
        float radius,
        float innerRadius,
        int? targetRaceId,
        float currentMorale,
        float elapsedSeconds)
    {
        ResistScaledRate = resistScaledRate;
        DistanceSq = distanceSq;
        Radius = radius;
        InnerRadius = innerRadius;
        TargetRaceId = targetRaceId;
        CurrentMorale = currentMorale;
        ElapsedSeconds = elapsedSeconds;
    }
}
