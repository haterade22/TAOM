using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SupplyLines.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// The order book. Orchestrates one order's life: consume from the source, spawn the caravan,
/// charge the player, advance hourly via <see cref="ISupplyOrderEngine"/> verdicts, deliver or
/// lose, purge.
///
/// <para>Payment ordering fixed relative to the source module: consume and spawn happen FIRST and
/// the charge lands only after the caravan exists, so a failed spawn refunds the consumed stock and
/// costs the player nothing (the module charged up front and kept the gold on failure).</para>
///
/// <para>Campaign statics (gold, rosters, encounter state, messages) are isolated behind protected
/// virtual members so every decision path is unit-testable; the virtual bodies themselves are the
/// untested boundary sliver.</para>
/// </summary>
public class SupplyOrderService : ISupplyOrderService
{
    private const string OrderIdPrefix = "taom_so_";

    private readonly ISupplySourceService _sources;
    private readonly ISupplyCaravanService _caravans;
    private readonly ISupplyOrderEngine _engine;
    private readonly ISupplyPricingService _pricing;
    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly IModLogger _logger;

    private Dictionary<string, SupplyOrder> _orders = new Dictionary<string, SupplyOrder>();
    private int _counter;

    public SupplyOrderService(
        ISupplySourceService sources,
        ISupplyCaravanService caravans,
        ISupplyOrderEngine engine,
        ISupplyPricingService pricing,
        ISupplyLinesSettingsProvider settings,
        IModLogger logger)
    {
        _sources = sources;
        _caravans = caravans;
        _engine = engine;
        _pricing = pricing;
        _settings = settings;
        _logger = logger;
    }

    public IReadOnlyCollection<SupplyOrder> ActiveOrders =>
        _orders.Values
            .Where(o => o.StatusEnum == SupplyOrderStatus.Ordered || o.StatusEnum == SupplyOrderStatus.InTransit)
            .ToList();

    public SupplyOrder TryPlaceOrder(
        SupplySourceInfo source,
        IReadOnlyDictionary<string, int> goods,
        IReadOnlyDictionary<string, int> troops,
        SupplyEscortOption escort,
        out string failReason)
    {
        failReason = null;
        if (!_settings.Enabled)
        {
            failReason = Localize(new TextObject("{=taom_sl_fail_disabled}Supply lines are disabled."));
            return null;
        }
        if (source == null)
        {
            failReason = Localize(new TextObject("{=taom_sl_fail_source}No supply source selected."));
            return null;
        }
        if (IsPlayerBlockaded())
        {
            failReason = Localize(new TextObject("{=taom_sl_fail_blockaded}You cannot arrange supplies while blockaded."));
            return null;
        }

        float distance = _sources.DistanceToPlayer(source);
        // Positive requirement: a NaN or unreachable distance must fail here, never become a free
        // instant delivery (the source module zeroed it).
        if (!FiniteFloatValidator.IsFinite(distance) || distance >= float.MaxValue)
        {
            failReason = Localize(new TextObject("{=taom_sl_fail_no_route}There is no route from that source to you right now."));
            return null;
        }

        // Lord sources ship no goods and never take an escort (source module behaviour).
        var effectiveEscort = string.IsNullOrEmpty(source.HeroId) ? escort : SupplyEscortOption.None;
        string escortHeroId = null;
        if (effectiveEscort == SupplyEscortOption.Companion)
        {
            escortHeroId = PickCompanionEscortId();
            if (escortHeroId == null)
                effectiveEscort = SupplyEscortOption.None; // no companion to send; order still valid
        }

        var consumption = _sources.Consume(
            source,
            goods ?? new Dictionary<string, int>(),
            troops ?? new Dictionary<string, int>());
        if (consumption == null || (consumption.Goods.Count == 0 && consumption.Troops.Count == 0))
        {
            failReason = Localize(new TextObject("{=taom_sl_fail_stock}Nothing you selected is still available at the source."));
            return null;
        }

        var quote = _pricing.Quote(consumption.GoodsMarketValue, consumption.TroopRecruitCost, distance, effectiveEscort);
        int total = quote.Total;
        if (PlayerGold < total)
        {
            RefundConsumption(source, consumption);
            failReason = Localize(new TextObject("{=taom_sl_fail_gold}Not enough gold to place this order."));
            return null;
        }

        var order = new SupplyOrder
        {
            OrderId = OrderIdPrefix + _counter++,
            SourceSettlementId = source.SettlementId,
            SourceHeroId = source.HeroId,
            Goods = new Dictionary<string, int>(consumption.Goods),
            Recruits = new Dictionary<string, int>(consumption.Troops),
            EscortHeroId = escortHeroId,
            TotalPaid = total,
            DispatchTime = CampaignTimeNow(),
            PlannedHours = _pricing.PlannedHours(distance),
        };
        order.EscortEnum = effectiveEscort;
        order.StatusEnum = SupplyOrderStatus.Ordered;

        string caravanPartyId = _caravans.Spawn(order);
        if (string.IsNullOrEmpty(caravanPartyId))
        {
            RefundConsumption(source, consumption);
            failReason = Localize(new TextObject("{=taom_sl_fail_spawn}The supply caravan could not be assembled."));
            return null;
        }

        // The caravan service already flips this when it records the party id; the belt here keeps
        // the order state machine correct even if that side effect ever changes.
        order.StatusEnum = SupplyOrderStatus.InTransit;

        // Charge only now, with the caravan alive on the map.
        ChargePlayer(total);
        _orders[order.OrderId] = order;
        ShowMessage(new TextObject("{=taom_sl_dispatched}A supply caravan has set out for your party."), error: false);
        return order;
    }

    public void HourlyTick()
    {
        if (_orders.Count == 0)
            return;
        AdvanceOrders();
        PurgeFinished();
    }

    public void FrameTick()
    {
        if (_orders.Count == 0)
            return;
        _caravans.TickPositions();
        // Proximity delivery reuses the same engine verdict as the hourly pass; the caravan
        // service's DistanceToPlayer is a cheap straight-line check so this is per-frame safe.
        if (AdvanceOrders())
            PurgeFinished();
    }

    public void CancelAll()
    {
        int cancelled = 0;
        foreach (var order in _orders.Values.ToList())
        {
            if (order.StatusEnum != SupplyOrderStatus.Ordered && order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;
            _caravans.ReleaseEscortAndDestroy(order);
            order.StatusEnum = SupplyOrderStatus.Lost;
            cancelled++;
        }
        PurgeFinished();
        if (cancelled > 0)
            ShowMessage(new TextObject("{=taom_sl_cancelled}Supply orders cancelled; the goods and gold are lost."), error: true);
    }

    public void LoadFrom(Dictionary<string, SupplyOrder> orders, int counter)
    {
        _orders = orders ?? new Dictionary<string, SupplyOrder>();
        _counter = Math.Max(counter, 0);
    }

    public void SaveInto(out Dictionary<string, SupplyOrder> orders, out int counter)
    {
        orders = _orders;
        counter = _counter;
    }

    public void OnGameLoaded()
    {
        _caravans.RespawnMissing(
            _orders.Values.Where(o => o.StatusEnum == SupplyOrderStatus.InTransit).ToList());
    }

    // --- verdict application ---

    /// <summary>Returns true when at least one order finished (delivered or lost).</summary>
    private bool AdvanceOrders()
    {
        bool anyFinished = false;
        bool playerInEncounter = IsPlayerInEncounter();
        foreach (var order in _orders.Values.ToList())
        {
            if (order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;
            var verdict = _engine.Advance(
                ElapsedFractionOf(order),
                _caravans.CaravanExists(order),
                _caravans.CaravanInRaid(order),
                _caravans.DistanceToPlayer(order),
                playerInEncounter);
            switch (verdict)
            {
                case SupplyOrderVerdict.Deliver:
                    DeliverOrder(order);
                    anyFinished = true;
                    break;
                case SupplyOrderVerdict.Lose:
                    LoseOrder(order);
                    anyFinished = true;
                    break;
            }
        }
        return anyFinished;
    }

    private void DeliverOrder(SupplyOrder order)
    {
        DeliverCargoToPlayer(order);
        _caravans.ReleaseEscortAndDestroy(order);
        order.StatusEnum = SupplyOrderStatus.Delivered;
        ShowMessage(new TextObject("{=taom_sl_delivered}Your supplies have arrived."), error: false);
    }

    private void LoseOrder(SupplyOrder order)
    {
        // Release attempts even without a live party; the caravan service checks reachability.
        _caravans.ReleaseEscortAndDestroy(order);
        order.StatusEnum = SupplyOrderStatus.Lost;
        ShowMessage(new TextObject("{=taom_sl_lost}A supply caravan was lost!"), error: true);
    }

    private void PurgeFinished()
    {
        foreach (var key in _orders
            .Where(kv => kv.Value.StatusEnum == SupplyOrderStatus.Delivered
                || kv.Value.StatusEnum == SupplyOrderStatus.Lost)
            .Select(kv => kv.Key)
            .ToList())
        {
            _orders.Remove(key);
        }
    }

    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

    protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;

    // TextObject.ToString needs the game's text manager for token processing; tests override this
    // to keep fail reasons deterministic outside the engine.
    protected virtual string Localize(TextObject text) => text.ToString();

    protected virtual CampaignTime CampaignTimeNow() => CampaignTime.Now;

    protected virtual float ElapsedFractionOf(SupplyOrder order) => order.ElapsedFraction();

    protected virtual void ChargePlayer(int amount)
    {
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);
    }

    protected virtual string PickCompanionEscortId()
    {
        return Hero.MainHero?.CompanionsInParty?.FirstOrDefault(h => h != null && h.IsAlive)?.StringId;
    }

    protected virtual bool IsPlayerBlockaded()
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return false;
        if (mainParty.MapEvent != null)
            return true;
        if (mainParty.BesiegedSettlement != null)
            return true;
        return mainParty.CurrentSettlement?.SiegeEvent != null;
    }

    protected virtual bool IsPlayerInEncounter()
    {
        return PlayerEncounter.Current != null || MobileParty.MainParty?.MapEvent != null;
    }

    protected virtual void DeliverCargoToPlayer(SupplyOrder order)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return;
        foreach (var pair in order.Goods)
        {
            var item = MBObjectManager.Instance.GetObject<ItemObject>(pair.Key);
            if (item != null && pair.Value > 0)
                mainParty.ItemRoster.AddToCounts(item, pair.Value);
            else if (item == null)
                _logger.LogWarning($"[SupplyLines] Deliver: unknown item id '{pair.Key}' dropped");
        }
        foreach (var pair in order.Recruits)
        {
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
            if (troop != null && pair.Value > 0)
                mainParty.MemberRoster.AddToCounts(troop, pair.Value);
            else if (troop == null)
                _logger.LogWarning($"[SupplyLines] Deliver: unknown troop id '{pair.Key}' dropped");
        }
    }

    /// <summary>
    /// Puts a consumption back where it came from after a failed placement: goods to the settlement
    /// roster, volunteers into empty notable slots, lord troops to the lord's roster. Best effort;
    /// a partial refund is logged rather than thrown because the player has not been charged.
    /// </summary>
    protected virtual void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
    {
        try
        {
            if (!string.IsNullOrEmpty(source.HeroId))
            {
                var roster = MBObjectManager.Instance.GetObject<Hero>(source.HeroId)?.PartyBelongedTo?.MemberRoster;
                if (roster == null)
                {
                    _logger.LogWarning($"[SupplyLines] refund: lord '{source.HeroId}' unreachable, troops not restored");
                    return;
                }
                foreach (var pair in consumption.Troops)
                {
                    var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
                    if (troop != null && pair.Value > 0)
                        roster.AddToCounts(troop, pair.Value);
                }
                return;
            }

            var settlement = Settlement.Find(source.SettlementId);
            if (settlement == null)
            {
                _logger.LogWarning($"[SupplyLines] refund: settlement '{source.SettlementId}' unreachable, stock not restored");
                return;
            }
            foreach (var pair in consumption.Goods)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(pair.Key);
                if (item != null && pair.Value > 0)
                    settlement.ItemRoster?.AddToCounts(item, pair.Value);
            }
            foreach (var pair in consumption.Troops)
            {
                var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
                if (troop == null || pair.Value <= 0)
                    continue;
                RestoreVolunteerSlots(settlement, troop, pair.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] refund after failed order placement threw: {ex.Message}");
        }
    }

    private static void RestoreVolunteerSlots(Settlement settlement, CharacterObject troop, int count)
    {
        if (settlement.Notables == null)
            return;
        int remaining = count;
        foreach (var notable in settlement.Notables)
        {
            var slots = notable?.VolunteerTypes;
            if (slots == null)
                continue;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = troop;
                    remaining--;
                }
            }
            if (remaining <= 0)
                return;
        }
    }

    protected virtual void ShowMessage(TextObject text, bool error)
    {
        InformationManager.DisplayMessage(
            new InformationMessage(text.ToString(), error ? Colors.Red : Colors.Green));
    }
}
