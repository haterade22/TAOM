namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingSettingsProvider
{
    bool EnableArmyStrategicIntelligence { get; }
    float CommitmentMultiplier { get; }
    float MaxPriorityBoost { get; }
    float EvilAggressionScale { get; }
    float LongRangePriorityBoostScale { get; }
}
