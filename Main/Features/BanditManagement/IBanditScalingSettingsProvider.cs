namespace TAOM.Features.BanditManagement;

public interface IBanditScalingSettingsProvider
{
    bool IsEnabled { get; }
    float DensityCurve { get; }
    float PartySizeCurve { get; }
    float BossFightCurve { get; }
    int MaxHideoutsPerFactionCap { get; }
    int MaxPartiesPerHideoutCap { get; }
    int MinPartiesToInfest { get; }
}
