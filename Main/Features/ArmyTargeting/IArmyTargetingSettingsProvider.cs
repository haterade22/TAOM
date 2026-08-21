namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingSettingsProvider
{
    bool EnableArmyStrategicIntelligence { get; }
    bool EnableWarTheaters { get; }
    float CommitmentMultiplier { get; }
    float MaxPriorityBoost { get; }
    float EvilAggressionScale { get; }
    float BorderProximityFloor { get; }

    /// <summary>
    /// Outer reach radius in town gaps. Settable from MCM; the config's inner radius is clamped
    /// against it inside the service so the two can never invert.
    /// </summary>
    float ReachRadiusInTownGaps { get; }

    /// <summary>Multiplier on Defender-mission target scores, the home-defence lever.</summary>
    float DefenderPriorityMultiplier { get; }
}
