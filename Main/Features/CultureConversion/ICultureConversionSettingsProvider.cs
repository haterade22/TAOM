namespace TAOM.Features.CultureConversion;

public interface ICultureConversionSettingsProvider
{
    bool IsEnabled { get; }
    int RequiredHoldDays { get; }
    bool RequireStableLoyalty { get; }
    float MinLoyaltyToConvert { get; }
    bool ConvertPlayerOwnedSettlements { get; }
}
