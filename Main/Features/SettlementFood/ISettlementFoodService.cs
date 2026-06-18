using TaleWorlds.CampaignSystem;

namespace TAOM.Features.SettlementFood;

public interface ISettlementFoodService
{
    /// <summary>
    /// Pure: the daily food adjustment to add on top of vanilla's <c>CalculateTownFoodStocksChange</c>.
    /// Combines the garrison raw-count correction (always) with the siege-gated production knobs.
    /// Returns 0 when <paramref name="enabled"/> is false.
    /// </summary>
    float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled);

    /// <summary>
    /// Applies <see cref="ComputeFoodDelta"/> to <paramref name="result"/> (the vanilla food
    /// ExplainedNumber). No-op when the delta is zero.
    /// </summary>
    void ApplyFoodAdjustment(
        TownFoodSnapshot snapshot,
        SettlementFoodConfig config,
        bool enabled,
        ref ExplainedNumber result,
        bool includeDescriptions);
}
