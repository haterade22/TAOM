namespace TAOM.Features.SettlementFood;

/// <summary>
/// Tunable knobs for <see cref="Models.TaomSettlementFoodModel"/>. Every default equals the vanilla
/// <c>DefaultSettlementFoodModel</c> constant, so an unedited config is behaviourally vanilla on the
/// production/consumption-rate side — the only always-on change is the garrison raw-count correction
/// (which neutralises the Troop Weight feature inflating garrison food consumption).
///
/// These are general tuning levers, not relief-only. Divisors are "men/prosperity per 1 food eaten":
/// RAISING relieves the deficit, LOWERING tightens it. The base/village knobs REPLACE the vanilla
/// constant (default = vanilla), so a value below vanilla LOWERS production — only <see cref="FlatFoodBonus"/>
/// is purely additive. Validation enforces finite, non-negative, divisors ≥ 1 (never that a value is
/// "≥ vanilla"). See docs/reference/engine/settlement-economy-food-prosperity.md.
/// </summary>
public class SettlementFoodConfig
{
    // Consumption divisors (vanilla: garrison 20, prosperity 40). Higher = less food eaten.
    public int GarrisonFoodDivisor { get; set; } = 20;
    public int ProsperityFoodDivisor { get; set; } = 40;

    // Base "lands around settlement" production (vanilla: town 15, castle 10).
    public float TownBaseFood { get; set; } = 15f;
    public float CastleBaseFood { get; set; } = 10f;

    // Per Normal-state bound village: (hearthLevel + 1) * multiplier (vanilla multiplier: 6).
    public float VillageFoodMultiplier { get; set; } = 6f;

    // Flat daily food added to every fortification (vanilla: 0). Siege-gated like all production.
    public float FlatFoodBonus { get; set; } = 0f;

    // Storage caps (vanilla: town limit 300, castle +150 bonus).
    public int FoodStocksUpperLimit { get; set; } = 300;
    public int CastleFoodStockUpperLimitBonus { get; set; } = 150;
}
