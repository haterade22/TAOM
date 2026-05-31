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

    public CastleRecruitmentSettingsProvider(ICastleRecruitmentConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableCastleRecruitment ?? _defaults.Enabled;

    public bool IsAiEnabled => TaomSettings.Instance?.EnableCastleRecruitmentAi ?? _defaults.AiEnabled;

    public int NotablesPerCastle =>
        SafeClampInt(TaomSettings.Instance?.CastleNotablesPerCastle, _defaults.NotablesPerCastle, 1, 5);

    private static int SafeClampInt(int? value, int defaultValue, int min, int max)
    {
        var v = value ?? defaultValue;
        return v < min ? min : v > max ? max : v;
    }
}
