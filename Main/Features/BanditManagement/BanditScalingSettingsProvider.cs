using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.BanditManagement;

/// <summary>
/// Reads the World / Bandit Scaling group live from <see cref="TaomSettings.Instance"/>. The
/// constants below are the fallback for the no-MCM case only; <see cref="TaomSettings"/> carries
/// the same values as its compiled defaults, and <c>BanditScalingSettingsProviderTests</c> pins the
/// two together. Until #559 these lived in a shipped <c>bandit_scaling_config.json</c> that MCM
/// shadowed on every real install and that nothing pinned, which players (reasonably) read as the
/// file the game obeys.
/// </summary>
public sealed class BanditScalingSettingsProvider : IBanditScalingSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    private const float DefaultDensityCurve = 1.5f;
    private const float DefaultPartySizeCurve = 1.5f;
    private const float DefaultBossFightCurve = 1.5f;
    private const int DefaultMaxHideoutsPerFactionCap = 100;
    private const int DefaultMaxPartiesPerHideoutCap = 6;
    private const int DefaultInitialHideoutsPerFaction = 7;
    private const int DefaultHideoutBossBodyguards = 4;

    // No MCM knob: vanilla needs 2 parties before a hideout counts as infested, TAOM needs 1 so
    // hideouts become active and visible sooner. Bounded by the live cap so min <= max holds even
    // if the player drags BanditPartiesPerHideoutCap to 1.
    private const int MinPartiesToInfestValue = 1;

    public bool IsEnabled => Settings?.EnableBanditScaling ?? true;

    public float DensityCurve =>
        SettingClamp.Clamp(Settings?.BanditDensityCurve, DefaultDensityCurve, 0f, 5f);

    public float PartySizeCurve =>
        SettingClamp.Clamp(Settings?.BanditPartySizeCurve, DefaultPartySizeCurve, 0f, 5f);

    public float BossFightCurve =>
        SettingClamp.Clamp(Settings?.BanditBossFightCurve, DefaultBossFightCurve, 0f, 5f);

    public int MaxHideoutsPerFactionCap =>
        SettingClamp.Clamp(Settings?.BanditMaxHideoutsPerFaction, DefaultMaxHideoutsPerFactionCap, 1, 100);

    public int MaxPartiesPerHideoutCap =>
        SettingClamp.Clamp(Settings?.BanditPartiesPerHideoutCap, DefaultMaxPartiesPerHideoutCap, 1, 20);

    public int InitialHideoutsPerFaction =>
        SettingClamp.Clamp(Settings?.BanditStartingHideoutsPerFaction, DefaultInitialHideoutsPerFaction, 1, 30);

    public int MinPartiesToInfest =>
        SettingClamp.Clamp(MinPartiesToInfestValue, MinPartiesToInfestValue, 1, MaxPartiesPerHideoutCap);

    public int HideoutBossBodyguards =>
        SettingClamp.Clamp(Settings?.BanditHideoutBossBodyguards, DefaultHideoutBossBodyguards, 0, 10);
}
