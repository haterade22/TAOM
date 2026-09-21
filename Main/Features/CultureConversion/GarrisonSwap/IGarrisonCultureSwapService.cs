namespace TAOM.Features.CultureConversion.GarrisonSwap;

/// <summary>
/// Re-mans a converted fief's standing troops in its new culture. Invoked from
/// <c>CultureConversionService.ApplyConversion</c>, once per settlement, at the moment the culture
/// flips — never from the on-load re-apply, which is what keeps the swap a one-shot and leaves
/// fiefs converted in older saves alone.
/// </summary>
public interface IGarrisonCultureSwapService
{
    /// <summary>
    /// Swaps the settlement's garrison and militia from <paramref name="fromCultureId"/> to
    /// <paramref name="toCultureId"/>, honouring the three toggles. Safe to call for a village
    /// (which simply has no garrison) and safe to call when nothing matches.
    /// </summary>
    /// <param name="fromCultureId">
    /// The settlement's culture BEFORE the flip. Needed because militia is matched slot-for-slot
    /// against the old culture's four militia troops, and by the time this runs
    /// <c>Settlement.Culture</c> already holds the new value.
    /// </param>
    /// <param name="isPlayerOwned">Gates the dedicated player-fief toggle, and whether the notice is shown.</param>
    void SwapSettlementTroops(string settlementId, string fromCultureId, string toCultureId, bool isPlayerOwned);
}
