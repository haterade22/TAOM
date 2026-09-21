namespace TAOM.Features.CultureConversion;

public class CultureConversionConfig
{
    /// <summary>Master toggle. When false, no new conversions are queued (existing overrides still re-apply on load).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Days the new owner must hold a cross-culture fief before it converts.</summary>
    public int RequiredHoldDays { get; set; } = 1;

    /// <summary>When true, conversion also waits for loyalty to reach <see cref="MinLoyaltyToConvert"/>.</summary>
    public bool RequireStableLoyalty { get; set; } = false;

    /// <summary>Loyalty floor (0–100) required to convert when <see cref="RequireStableLoyalty"/> is true.</summary>
    public float MinLoyaltyToConvert { get; set; } = 50f;

    /// <summary>When false, settlements owned by the player's clan never convert (AI conquests still do).</summary>
    public bool ConvertPlayerOwnedSettlements { get; set; } = true;

    /// <summary>
    /// When true, conversion also replaces each notable whose culture differs from the new culture
    /// with a same-occupation notable from the new culture's templates (property transfers, relations reset).
    /// </summary>
    public bool ReplaceNotablesOnConversion { get; set; } = true;

    /// <summary>
    /// When true, conversion re-mans the fief's standing garrison with the new culture's equivalent
    /// troops (same tier, same battlefield role, same head count). Vanilla never does this: only a
    /// siege clears a garrison, and every peaceful transfer keeps the old culture's troops forever.
    /// </summary>
    public bool ReplaceGarrisonOnConversion { get; set; } = true;

    /// <summary>
    /// When true, conversion also swaps the settlement's (and each bound village's) militia to the
    /// new culture's militia troops, slot for slot. New militia already spawn correctly once the
    /// culture flips; this makes the existing stack catch up immediately instead of over many days.
    /// </summary>
    public bool ReplaceMilitiaOnConversion { get; set; } = true;

    /// <summary>
    /// When false, the two swaps above never touch a fief owned by the player's clan (AI fiefs still
    /// swap). Separate from <see cref="ConvertPlayerOwnedSettlements"/> because discarding a garrison
    /// the player stacked by hand is a far bigger imposition than flipping the fief's culture.
    /// </summary>
    public bool ReplaceGarrisonInPlayerFiefs { get; set; } = true;
}
