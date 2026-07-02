namespace TAOM.Features.SettlementEconomy;

/// <summary>
/// Tunable knobs for <see cref="Models.TaomSettlementEconomyModel"/>. The engine regenerates a
/// town's market gold daily toward <c>base + prosperity × perProsperity</c> at <c>rate</c> of the
/// deficit per day (vanilla: 10000 / 12 / 0.25 — <c>DefaultSettlementEconomyModel.GetTownGoldChange</c>).
///
/// Unlike SettlementFood, the shipped defaults are NOT vanilla: <see cref="TownGoldBase"/> is buffed
/// to 25000 (#317) because TAOM's drains run ~2× vanilla (2.2× LOTRLOME loot values + 22% more
/// villager deliveries) — a base-heavy buff gives prosperity-collapsed towns 2.5× faster recovery
/// while a median town gains only ~29%. Slope and rate stay vanilla: slope-buffing rewards prosperity
/// the broke towns lack, and rate changes only the loot-farm extraction ceiling, not the equilibrium.
/// Invalid values revert to these shipped defaults (not vanilla). MCM master toggle off = vanilla
/// engine math via base-call passthrough.
/// </summary>
public class SettlementEconomyConfig
{
    // Equilibrium target = TownGoldBase + prosperity * TownGoldPerProsperity (vanilla: 10000 + P*12).
    public float TownGoldBase { get; set; } = 25000f;
    public float TownGoldPerProsperity { get; set; } = 12f;

    // Fraction of the deficit recovered per day (vanilla: 0.25). Also the mean-reversion rate when
    // a town sits ABOVE its target. 0 = freeze regen (explicit extreme; validated bound [0,1]).
    public float TownGoldRegenRate { get; set; } = 0.25f;
}
