namespace TAOM.Features.CaravanTrade;

/// <summary>
/// JSON DTO for <c>caravan_trade/caravan_trade_config.json</c>. Defaults are the shipped tuning.
/// Validated field-by-field by <see cref="CaravanTradeConfigProvider"/>; the MCM layer overrides a
/// subset at runtime via <see cref="ICaravanTradeSettingsProvider"/>.
/// </summary>
public class CaravanTradeConfig
{
    public bool Enabled { get; set; } = true;
    public bool ApplyToPlayerCaravans { get; set; } = true;

    // Range envelope + selection re-weight (Levers 2 & 3).
    public float RangeMultiplier { get; set; } = 1.6f;
    public float DistanceDecayExponent { get; set; } = 0.5f;
    public float NearFieldFlattenDays { get; set; } = 2.0f;
    public float MaxCompensation { get; set; } = 6.0f;
    public float AntiShuttlePenalty { get; set; } = 0.35f;

    // War policy (Lever 1). Validated against the known set; unknown reverts to the default.
    public string WarTradePolicy { get; set; } = "SameAlignmentAndNeutral";

    // Basket diversity (Lever 4).
    public float BudgetFactorFloor { get; set; } = 0.35f;
    public int InitialTradeGold { get; set; } = 15000;
    public int MaxGoldPerCategory { get; set; } = 1500;
}

/// <summary>Parsing + known-set validation for the <see cref="WarTradePolicy"/> config string (the M1 typo trap).</summary>
public static class WarTradePolicyParser
{
    /// <summary>Case-insensitively parse a config string into a <see cref="WarTradePolicy"/>. Returns false for null/empty/unknown.</summary>
    public static bool TryParse(string value, out WarTradePolicy policy)
    {
        policy = WarTradePolicy.SameAlignmentAndNeutral;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
                policy = WarTradePolicy.None;
                return true;
            case "ignorewar":
                policy = WarTradePolicy.IgnoreWar;
                return true;
            case "samealignmentandneutral":
                policy = WarTradePolicy.SameAlignmentAndNeutral;
                return true;
            default:
                return false;
        }
    }
}
