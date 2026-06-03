namespace TAOM.Features.CultureConversion;

public class CultureConversionConfig
{
    /// <summary>Master toggle. When false, no new conversions are queued (existing overrides still re-apply on load).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Days the new owner must hold a cross-culture fief before it converts.</summary>
    public int RequiredHoldDays { get; set; } = 45;

    /// <summary>When true, conversion also waits for loyalty to reach <see cref="MinLoyaltyToConvert"/>.</summary>
    public bool RequireStableLoyalty { get; set; } = false;

    /// <summary>Loyalty floor (0–100) required to convert when <see cref="RequireStableLoyalty"/> is true.</summary>
    public float MinLoyaltyToConvert { get; set; } = 50f;

    /// <summary>When false, settlements owned by the player's clan never convert (AI conquests still do).</summary>
    public bool ConvertPlayerOwnedSettlements { get; set; } = true;
}
