namespace TAOM.Features.QuickActions.Models;

public enum DamagedQualityPreset
{
    Pristine = 0,
    Slight = 1,
    Moderate = 2,
    Heavy = 3,
}

public static class DamagedQualityPresetExtensions
{
    /// <summary>
    /// Returns the threshold against which we test <c>(modifier.PriceMultiplier - 1f)</c>.
    /// An item is "damaged at this preset" when <c>(priceMultiplier - 1f) &lt;= threshold</c>.
    /// Pristine returns 0f (effectively never matches; reserved as a sentinel).
    /// </summary>
    public static float ToThreshold(this DamagedQualityPreset preset) => preset switch
    {
        DamagedQualityPreset.Pristine => 0f,
        DamagedQualityPreset.Slight => -0.10f,
        DamagedQualityPreset.Moderate => -0.20f,
        DamagedQualityPreset.Heavy => -0.40f,
        _ => -0.20f,
    };

    public static DamagedQualityPreset FromDropdownIndex(int index) => index switch
    {
        0 => DamagedQualityPreset.Pristine,
        1 => DamagedQualityPreset.Slight,
        2 => DamagedQualityPreset.Moderate,
        3 => DamagedQualityPreset.Heavy,
        _ => DamagedQualityPreset.Moderate,
    };
}
