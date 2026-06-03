namespace TAOM.Features.CultureConversion;

public interface ICultureConversionService
{
    /// <summary>Called when a town/castle changes owner. Starts/cancels a conversion timer as appropriate.</summary>
    void OnSettlementConquered(string settlementId, double nowDays);

    /// <summary>Daily sweep: completes any pending conversion whose hold period (and loyalty gate) is satisfied.</summary>
    void RunDailyChecks(double nowDays);

    /// <summary>Re-applies completed culture overrides after a save load (Settlement.Culture is not engine-saved).</summary>
    void ReapplyConvertedCultures();
}
