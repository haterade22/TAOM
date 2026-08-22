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
    /// How far, in town gaps, Patch22 may reach when overturning vanilla's "unreachable" verdict
    /// for a priority-list target. Not a general distance penalty: vanilla already applies one.
    /// </summary>
    float BorderRescueRadiusInTownGaps { get; }

    /// <summary>Multiplier on Defender-mission target scores, the home-defence lever.</summary>
    float DefenderPriorityMultiplier { get; }
}
