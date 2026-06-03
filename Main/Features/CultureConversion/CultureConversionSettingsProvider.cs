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

    public CultureConversionSettingsProvider(ICultureConversionConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableCultureConversion ?? _defaults.Enabled;

    public int RequiredHoldDays =>
        SafeClampInt(TaomSettings.Instance?.CultureConversionHoldDays, _defaults.RequiredHoldDays, MinHoldDays, MaxHoldDays);

    public bool RequireStableLoyalty =>
        TaomSettings.Instance?.CultureConversionRequireStableLoyalty ?? _defaults.RequireStableLoyalty;

    public float MinLoyaltyToConvert => _defaults.MinLoyaltyToConvert;

    public bool ConvertPlayerOwnedSettlements => _defaults.ConvertPlayerOwnedSettlements;

    private static int SafeClampInt(int? value, int defaultValue, int min, int max)
    {
        var v = value ?? defaultValue;
        return v < min ? min : v > max ? max : v;
    }
}
