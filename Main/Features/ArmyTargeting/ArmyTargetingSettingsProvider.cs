namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingSettingsProvider : IArmyTargetingSettingsProvider
{
    public bool EnableArmyStrategicIntelligence => TaomSettings.Instance?.EnableArmyStrategicIntelligence ?? true;
    public bool EnableWarTheaters               => TaomSettings.Instance?.EnableWarTheaters              ?? true;
    public float CommitmentMultiplier           => TaomSettings.Instance?.ArmyCommitmentMultiplier       ?? 4.0f;
    public float MaxPriorityBoost               => TaomSettings.Instance?.ArmyPriorityBoost              ?? 3.0f;
    public float EvilAggressionScale            => TaomSettings.Instance?.EvilFactionAggressionScale     ?? 1.0f;
    public float BorderProximityFloor           => TaomSettings.Instance?.ArmyBorderProximityFloor       ?? 0.15f;
    public float ReachRadiusInTownGaps          => TaomSettings.Instance?.ArmyReachRadiusInTownGaps      ?? 3.0f;
    public float DefenderPriorityMultiplier     => TaomSettings.Instance?.ArmyDefenderPriority           ?? 1.6f;
}
