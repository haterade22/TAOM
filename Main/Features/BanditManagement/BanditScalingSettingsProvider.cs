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
    private const float DefaultDensityCurve = 1.5f;
    private const float DefaultPartySizeCurve = 1.5f;
    private const float DefaultBossFightCurve = 1.5f;
    private const int DefaultMaxHideoutsPerFactionCap = 100;
    private const int DefaultMaxPartiesPerHideoutCap = 6;
    private const int DefaultInitialHideoutsPerFaction = 7;
    private const int DefaultHideoutBossBodyguards = 4;

    // No MCM knob: vanilla needs 2 parties before a hideout counts as infested, TAOM needs 1 so
    // hideouts become active and visible sooner. Bounded by the live cap so min <= max holds even
    // if the player drags BanditMaxPartiesPerHideout to 1.
    private const int MinPartiesToInfestValue = 1;

    public bool IsEnabled => TaomSettings.Instance?.EnableBanditScaling ?? true;

    public float DensityCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditDensityCurve, DefaultDensityCurve, 0f, 5f);

    public float PartySizeCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditPartySizeCurve, DefaultPartySizeCurve, 0f, 5f);

    public float BossFightCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditBossFightCurve, DefaultBossFightCurve, 0f, 5f);

    public int MaxHideoutsPerFactionCap =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditMaxHideoutsPerFaction, DefaultMaxHideoutsPerFactionCap, 1, 100);

    public int MaxPartiesPerHideoutCap =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditMaxPartiesPerHideout, DefaultMaxPartiesPerHideoutCap, 1, 20);

    public int InitialHideoutsPerFaction =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditInitialHideoutsPerFaction, DefaultInitialHideoutsPerFaction, 1, 30);

    public int MinPartiesToInfest =>
        SettingClamp.Clamp(MinPartiesToInfestValue, MinPartiesToInfestValue, 1, MaxPartiesPerHideoutCap);

    public int HideoutBossBodyguards =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditHideoutBossBodyguards, DefaultHideoutBossBodyguards, 0, 10);
}
