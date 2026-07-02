using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.SettlementEconomy.Models;

/// <summary>
/// Tunable town market-gold regeneration (#317 — "towns run out of gold and never recover").
///
/// Overrides ONLY <see cref="GetTownGoldChange"/>: the engine regenerates a town's market gold
/// daily toward <c>base + prosperity × perProsperity</c> at <c>rate</c> of the deficit per day.
/// TAOM's drains run ~2× vanilla (2.2× LOTRLOME loot values, +22% villager deliveries), so the
/// shipped config buffs the base (25000 vs vanilla 10000) — see <see cref="SettlementEconomyConfig"/>.
///
/// Castles never reach this override: the sole engine caller is
/// <c>ItemConsumptionBehavior.UpdateTownGold</c> (v1.4.6 :73-77), reached only via
/// <c>DailyTickTownEvent</c>, which iterates <c>Town.AllTowns</c> (towns only — castles live in
/// <c>Town.AllCastles</c>, never ticked here). Re-verify that chain on the next engine bump before
/// adding any castle logic. Toggle off ⇒ base-call passthrough = vanilla math, drift-safe across
/// engine formula changes.
/// </summary>
public class TaomSettlementEconomyModel : DefaultSettlementEconomyModel
{
    private readonly ISettlementEconomyService _service;
    private readonly SettlementEconomyConfig _config;

    public TaomSettlementEconomyModel(ISettlementEconomyService service, ISettlementEconomyConfigProvider configProvider)
    {
        _service = service;
        _config = configProvider.GetConfig();
    }

    private bool Enabled => TaomSettings.Instance?.EnableSettlementEconomyTuning ?? true;

    public override int GetTownGoldChange(Town town)
    {
        if (town == null)
            return 0;

        if (!Enabled)
            return base.GetTownGoldChange(town);

        return _service.ComputeTownGoldChange(town.Prosperity, town.Gold, _config);
    }
}
