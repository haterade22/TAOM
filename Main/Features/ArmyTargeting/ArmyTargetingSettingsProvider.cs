namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingSettingsProvider : IArmyTargetingSettingsProvider
{
    public bool EnableArmyStrategicIntelligence => TaomSettings.Instance?.EnableArmyStrategicIntelligence ?? true;
    public float CommitmentMultiplier           => TaomSettings.Instance?.ArmyCommitmentMultiplier        ?? 4.0f;
    public float MaxPriorityBoost               => TaomSettings.Instance?.ArmyPriorityBoost               ?? 3.0f;
}
