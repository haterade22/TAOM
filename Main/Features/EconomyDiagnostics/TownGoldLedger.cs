using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.EconomyDiagnostics;

/// <summary>
/// Who moved a town's market gold. Named after the engine site, so a report line maps straight to
/// code rather than to a guess.
/// </summary>
public enum TownGoldFlow
{
    /// <summary>
    /// Nothing claimed it. Not a throwaway bucket — a large <c>Other</c> is the useful signal that
    /// a drain exists outside the sites tagged below, and the next thing to instrument.
    ///
    /// Known occupants, all verified as reaching <c>ChangeGold</c> without passing a tagged entry
    /// point:
    /// <list type="bullet">
    /// <item><b>The player's own market trades</b> — <c>InventoryLogic.DoneLogic</c> →
    /// <c>MerchantInventoryListener.SetGold</c> → <c>ChangeGold</c> (<c>InventoryScreenHelper.cs:128</c>),
    /// never via <c>SellItemsAction</c>. Note the asymmetry: a player SALE is clamped by the
    /// zero-floor, but a player PURCHASE credits the town uncapped and can mask a real drain in the
    /// report's NET line.</item>
    /// <item><c>WorkshopsCampaignBehavior</c> (:853, :868) — deliberately untagged.</item>
    /// <item><c>BanditSpawnCampaignBehavior</c> :178 — 25% of looted value returned.</item>
    /// <item><c>Town.OnInit</c> :485 — the one-off 20,000 starting balance.</item>
    /// </list>
    /// </summary>
    Other,

    /// <summary>`DefaultSettlementEconomyModel.GetTownGoldChange` via `ItemConsumptionBehavior.UpdateTownGold`.</summary>
    DailyMint,

    /// <summary>Residents buying shelf stock — `ItemConsumptionBehavior.MakeConsumption`.</summary>
    ResidentConsumption,

    /// <summary>`SellGoodsForTradeAction` — the unbounded drain; it can spend a town to zero daily.</summary>
    VillagerDelivery,

    /// <summary>
    /// `SellItemsAction` — **AI parties only**, in both directions. All seven of that method's
    /// callers in v1.4.7 gate on `IsMainParty` / `!= MainParty`: caravan sales and purchases
    /// (`CaravansCampaignBehavior` :1202, :1336), AI party horse purchases
    /// (`PartiesBuyHorseCampaignBehavior` :103, :126, :172), AI party food purchases
    /// (`PartiesBuyFoodCampaignBehavior` :69) and AI lord loot sales
    /// (`PartiesSellLootCampaignBehavior` :38).
    ///
    /// Emphatically NOT the player's own market trades — those never touch this action at all and
    /// land in <see cref="Other"/>. Calling this bucket "player trades" would send an investigator
    /// after the wrong drain.
    /// </summary>
    Trade,
}

/// <summary>One day's gold movement for one town, broken out by who moved it.</summary>
public class TownGoldDay
{
    private readonly Dictionary<TownGoldFlow, int> _byFlow = new Dictionary<TownGoldFlow, int>();

    internal void Add(TownGoldFlow flow, int delta)
    {
        _byFlow.TryGetValue(flow, out var current);
        _byFlow[flow] = current + delta;
    }

    public int AmountFor(TownGoldFlow flow) => _byFlow.TryGetValue(flow, out var v) ? v : 0;

    public int Net => _byFlow.Values.Sum();

    public IEnumerable<TownGoldFlow> RecordedFlows => _byFlow.Keys;
}

/// <summary>
/// A bounded per-town, per-day ledger of market-gold movement.
///
/// The seam that makes this trustworthy: <c>SettlementComponent.Gold</c> has a private setter and
/// <c>ChangeGold(int)</c> is its only mutator, so a single recorder there observes every flow with
/// no possibility of a missed site. Attribution comes from <see cref="TownGoldFlowScope"/>, an
/// ambient tag set by thin prefixes on the known callers.
///
/// Ephemeral by design — no <c>SyncData</c>. It is a diagnostic, and persisting it would put
/// churning per-day buckets into the save file for no gameplay benefit.
///
/// Thread-safety is not needed and deliberately not added: every campaign ticker that reaches
/// <c>ChangeGold</c> is registered with <c>doParallel: false</c> in
/// <c>CampaignPeriodicEventManager.InitializeTickers</c> (verified against v1.4.7), so all recording
/// happens on the main tick thread.
/// </summary>
public class TownGoldLedger : ITownGoldLedger
{
    private const int DefaultDaysRetained = 7;

    private readonly int _daysRetained;
    private readonly Dictionary<string, List<TownGoldDay>> _history =
        new Dictionary<string, List<TownGoldDay>>();

    /// <summary>
    /// The only PUBLIC constructor, and deliberately so: DryIoc selects a constructor by reflection
    /// over public ones and throws `UnableToSelectSinglePublicConstructorFromMultiple` at
    /// registration when there is more than one — which takes the whole game down in
    /// `SubModule.OnSubModuleLoad`, before the main menu. The retention overload below is `internal`
    /// so tests can pick a short window without re-introducing that ambiguity.
    /// </summary>
    public TownGoldLedger() : this(DefaultDaysRetained) { }

    internal TownGoldLedger(int daysRetained)
    {
        // A non-positive window would make every read empty, which reads as "no drain" — the exact
        // wrong conclusion. Fall back rather than honour it.
        _daysRetained = daysRetained > 0 ? daysRetained : DefaultDaysRetained;
    }

    public void Record(string settlementId, TownGoldFlow flow, int delta)
    {
        // The recorder runs for every settlement on every tick, including half-built ones during
        // world init, and ChangeGold(0) is a common no-op. Neither should create a bucket.
        if (string.IsNullOrWhiteSpace(settlementId) || delta == 0) return;

        if (!_history.TryGetValue(settlementId, out var days))
        {
            days = new List<TownGoldDay> { new TownGoldDay() };
            _history[settlementId] = days;
        }

        days[days.Count - 1].Add(flow, delta);
    }

    public void BeginDay()
    {
        foreach (var days in _history.Values)
        {
            days.Add(new TownGoldDay());

            // Bounded ring: a long campaign must not grow this without limit.
            while (days.Count > _daysRetained)
                days.RemoveAt(0);
        }
    }

    /// <summary>Newest day first — the reader almost always wants yesterday, not day one.</summary>
    public IReadOnlyList<TownGoldDay> GetHistory(string settlementId)
    {
        if (settlementId == null || !_history.TryGetValue(settlementId, out var days))
            return Array.Empty<TownGoldDay>();

        var newestFirst = new List<TownGoldDay>(days);
        newestFirst.Reverse();
        return newestFirst;
    }

    public TownGoldDay GetTotals(string settlementId)
    {
        var totals = new TownGoldDay();

        foreach (var day in GetHistory(settlementId))
        foreach (var flow in day.RecordedFlows)
            totals.Add(flow, day.AmountFor(flow));

        return totals;
    }

    /// <summary>
    /// Called at session launch. Settlement ids repeat across campaigns and this is a process-level
    /// singleton, so without this a fresh campaign inherits an abandoned one's buckets — the same
    /// cross-campaign leak already found and fixed once in the caravan visit memory.
    /// </summary>
    public void ClearAll() => _history.Clear();
}
