using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>BanditScalingSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null very early in
/// startup or if MCM fails to load — the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class CastleRecruitmentSettingsProvider : ICastleRecruitmentSettingsProvider
{
    private readonly CastleRecruitmentConfig _defaults;

    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CastleRecruitmentSettingsProvider(ICastleRecruitmentConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    internal CastleRecruitmentSettingsProvider(ICastleRecruitmentConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;

    public bool IsEnabled => Settings?.EnableCastleRecruitment ?? _defaults.Enabled;

    public bool IsAiEnabled => Settings?.EnableCastleRecruitmentAi ?? _defaults.AiEnabled;

    public int NotablesPerCastle =>
        SettingClamp.Clamp(Settings?.CastleNotablesPerCastle, _defaults.NotablesPerCastle, 1, 5);
}
