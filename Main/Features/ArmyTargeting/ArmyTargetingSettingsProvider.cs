namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingSettingsProvider : IArmyTargetingSettingsProvider
{
    public bool EnableArmyStrategicIntelligence => TaomSettings.Instance?.EnableArmyStrategicIntelligence ?? true;
    public float CommitmentMultiplier           => TaomSettings.Instance?.ArmyCommitmentMultiplier        ?? 4.0f;
    public float MaxPriorityBoost               => TaomSettings.Instance?.ArmyPriorityBoost               ?? 3.0f;
    public float EvilAggressionScale            => TaomSettings.Instance?.EvilFactionAggressionScale      ?? 1.0f;
    public float LongRangePriorityBoostScale    => TaomSettings.Instance?.LongRangePriorityBoostScale     ?? 1.0f;
}
