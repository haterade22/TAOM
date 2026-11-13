using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.CultureConversion;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>CastleRecruitmentSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null early in
/// startup or if MCM fails — the <c>?? default</c> fallback keeps every read safe. MinLoyaltyToConvert
/// and ConvertPlayerOwnedSettlements are JSON-only (advanced); the rest have MCM knobs.
/// </summary>
public sealed class CultureConversionSettingsProvider : ICultureConversionSettingsProvider
{
    private const int MinHoldDays = 1;
    private const int MaxHoldDays = 100000;

    private readonly CultureConversionConfig _defaults;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CultureConversionSettingsProvider(ICultureConversionConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => Settings?.EnableCultureConversion ?? _defaults.Enabled;

    public int RequiredHoldDays =>
        SettingClamp.Clamp(Settings?.CultureConversionHoldDays, _defaults.RequiredHoldDays, MinHoldDays, MaxHoldDays);

    public bool RequireStableLoyalty =>
        Settings?.CultureConversionRequireStableLoyalty ?? _defaults.RequireStableLoyalty;

    public float MinLoyaltyToConvert => _defaults.MinLoyaltyToConvert;

    public bool ConvertPlayerOwnedSettlements => _defaults.ConvertPlayerOwnedSettlements;

    public bool ReplaceNotablesOnConversion =>
        Settings?.CultureConversionReplaceNotables ?? _defaults.ReplaceNotablesOnConversion;

    public bool ReplaceGarrisonOnConversion =>
        Settings?.CultureConversionReplaceGarrison ?? _defaults.ReplaceGarrisonOnConversion;

    public bool ReplaceMilitiaOnConversion =>
        Settings?.CultureConversionReplaceMilitia ?? _defaults.ReplaceMilitiaOnConversion;

    public bool ReplaceGarrisonInPlayerFiefs =>
        Settings?.CultureConversionReplaceGarrisonPlayerFiefs ?? _defaults.ReplaceGarrisonInPlayerFiefs;
}
