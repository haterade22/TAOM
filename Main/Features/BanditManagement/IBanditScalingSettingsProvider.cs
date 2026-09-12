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
    int InitialHideoutsPerFaction { get; }

    /// <summary>Soldiers beside the boss in the hideout boss fight, both routes. MCM-clamped to
    /// <c>[0, 10]</c>; independent of <see cref="IsEnabled"/> (#564).</summary>
    int HideoutBossBodyguards { get; }
}
