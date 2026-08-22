using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Engine-boundary implementation of <see cref="ISupplySourceService"/>: enumerates orderable
/// settlements and lords, lists their stock, and performs the consume on confirm.
///
/// <para>Two source-module defects fixed here rather than ported: goods are DEDUCTED from the
/// settlement roster when consumed (the module conjured them, duplicating stock), and volunteer
/// recruiting honours <c>VolunteerModel.MaximumIndexHeroCanRecruitFromHero</c> so TAOM's alignment
/// gate (-1 = recruit nothing from this notable) applies to supply orders too.</para>
/// </summary>
public sealed class SupplySourceService : ISupplySourceService
{
    // An at-war settlement still appears in the list (disabled, with a reason) when it is within
    // this map distance, so the player can see why the market next door is closed to them.
    private const float NearbyEnemyRadius = 40f;

    // Friendly lords further away than this are not worth a messenger; matches the source module.
    private const float LordMessengerRadius = 80f;

    // The order screen shows at most this many goods rows; we keep the most valuable ones.
    private const int MaxGoodsRows = 14;

    private readonly ISupplyPricingService _pricing;
    private readonly IModLogger _logger;

    public SupplySourceService(ISupplyPricingService pricing, IModLogger logger)
    {
        _pricing = pricing;
        _logger = logger;
    }

    public IReadOnlyList<SupplySourceInfo> GetSources()
    {
        var result = new List<SupplySourceInfo>();
        var mainHero = Hero.MainHero;
        if (mainHero?.MapFaction == null || MobileParty.MainParty == null)
            return result;

        var settlements = new List<SupplySourceInfo>();
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                continue;
            if (settlement.IsUnderSiege)
                continue;
            var ownerFaction = settlement.OwnerClan?.MapFaction;
            if (ownerFaction == null)
                continue;

            bool atWar = ownerFaction != mainHero.MapFaction && ownerFaction.IsAtWarWith(mainHero.MapFaction);
            float distance = DistanceToSettlement(settlement);
            if (atWar && distance > NearbyEnemyRadius)
                continue;

            settlements.Add(new SupplySourceInfo
            {
                SettlementId = settlement.StringId,
                DisplayName = settlement.Name?.ToString() ?? settlement.StringId,
                RelationText = RelationLabel(settlement, mainHero, atWar),
                Distance = distance,
                CanOrder = !atWar,
                DisabledReason = atWar
                    ? new TextObject("{=taom_sl_at_war}At war, you cannot trade here.").ToString()
                    : null,
            });
        }
        settlements.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        result.AddRange(settlements);

        result.AddRange(GetLordSources(mainHero));
        return result;
    }

    public IReadOnlyList<SupplyLineItem> GetGoods(SupplySourceInfo source)
    {
        var result = new List<SupplyLineItem>();
        if (string.IsNullOrEmpty(source?.SettlementId))
            return result;
        var settlement = Settlement.Find(source.SettlementId);
        if (settlement == null)
            return result;

        var roster = settlement.ItemRoster;
        if (roster == null)
            return result;

        var rows = new List<SupplyLineItem>();
        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var item = element.EquipmentElement.Item;
            int amount = element.Amount;
            if (item == null || amount <= 0 || !item.IsFood)
                continue;
            rows.Add(new SupplyLineItem
            {
                Id = item.StringId,
                Name = item.Name?.ToString() ?? item.StringId,
                Available = amount,
                UnitPrice = GetItemMarketValue(settlement, item),
            });
        }

        // Most valuable first; the screen caps at MaxGoodsRows rows.
        rows.Sort((a, b) => b.UnitPrice.CompareTo(a.UnitPrice));
        for (int i = 0; i < rows.Count && i < MaxGoodsRows; i++)
            result.Add(rows[i]);
        return result;
    }

    public IReadOnlyList<SupplyLineItem> GetTroops(SupplySourceInfo source)
    {
        var result = new List<SupplyLineItem>();
        if (source == null)
            return result;

        bool atWar = IsPlayerAtWarWithAnyKingdom();

        if (!string.IsNullOrEmpty(source.HeroId))
        {
            var lord = MBObjectManager.Instance.GetObject<Hero>(source.HeroId);
            var roster = lord?.PartyBelongedTo?.MemberRoster;
            if (roster == null)
                return result;
            for (int i = 0; i < roster.Count; i++)
            {
                var troop = roster.GetCharacterAtIndex(i);
                int count = roster.GetElementNumber(i);
                if (troop == null || troop.IsHero || count <= 0)
                    continue;
                result.Add(new SupplyLineItem
                {
                    Id = troop.StringId,
                    Name = troop.Name?.ToString() ?? troop.StringId,
                    Available = count,
                    UnitPrice = TroopUnitPrice(troop, atWar),
                });
            }
            return result;
        }

        var settlement = Settlement.Find(source.SettlementId);
        if (settlement == null)
            return result;

        var counts = new Dictionary<CharacterObject, int>();
        ForEachRecruitableSlot(settlement, (notable, slotIndex, troop) =>
        {
            counts.TryGetValue(troop, out int existing);
            counts[troop] = existing + 1;
            return false; // never consume while listing
        });

        foreach (var pair in counts)
        {
            result.Add(new SupplyLineItem
            {
                Id = pair.Key.StringId,
                Name = pair.Key.Name?.ToString() ?? pair.Key.StringId,
                Available = pair.Value,
                UnitPrice = TroopUnitPrice(pair.Key, atWar),
            });
        }
        return result;
    }

    public float DistanceToPlayer(SupplySourceInfo source)
    {
        if (source == null)
            return float.MaxValue;
        if (!string.IsNullOrEmpty(source.HeroId))
        {
            var lord = MBObjectManager.Instance.GetObject<Hero>(source.HeroId);
            return DistanceToLord(lord);
        }
        var settlement = Settlement.Find(source.SettlementId);
        return settlement == null ? float.MaxValue : DistanceToSettlement(settlement);
    }

    public SupplyConsumption Consume(
        SupplySourceInfo source,
        IReadOnlyDictionary<string, int> goods,
        IReadOnlyDictionary<string, int> troops)
    {
        var result = new SupplyConsumption();
        if (source == null)
            return result;

        bool atWar = IsPlayerAtWarWithAnyKingdom();

        if (!string.IsNullOrEmpty(source.HeroId))
        {
            ConsumeFromLord(source.HeroId, troops, atWar, result);
            return result;
        }

        var settlement = Settlement.Find(source.SettlementId);
        if (settlement == null)
        {
            _logger.LogWarning($"[SupplyLines] Consume: source settlement '{source.SettlementId}' not found, nothing taken");
            return result;
        }

        ConsumeGoodsFromSettlement(settlement, goods, result);
        ConsumeVolunteersFromSettlement(settlement, troops, atWar, result);
        return result;
    }

    // --- goods ---

    private void ConsumeGoodsFromSettlement(
        Settlement settlement, IReadOnlyDictionary<string, int> goods, SupplyConsumption result)
    {
        if (goods == null || goods.Count == 0)
            return;
        var roster = settlement.ItemRoster;
        if (roster == null)
            return;

        foreach (var pair in goods)
        {
            if (pair.Value <= 0)
                continue;
            // Validate the id before anything else; GetObject returns null for unknown ids and we
            // must never let a bad id silently become "zero of something else" downstream.
            var item = MBObjectManager.Instance.GetObject<ItemObject>(pair.Key);
            if (item == null)
            {
                _logger.LogWarning($"[SupplyLines] Consume: unknown item id '{pair.Key}' skipped");
                continue;
            }
            int present = roster.GetItemNumber(item);
            int take = Math.Min(pair.Value, present);
            if (take <= 0)
                continue;

            // Deduct from the settlement and price at the moment of deduction. The source module
            // never removed the goods (an economy dupe) and priced from a stale screen snapshot.
            int unitPrice = GetItemMarketValue(settlement, item);
            roster.AddToCounts(item, -take);
            result.Goods[pair.Key] = take;
            result.GoodsMarketValue += take * (float)unitPrice;
        }
    }

    // --- troops ---

    private void ConsumeFromLord(
        string heroId, IReadOnlyDictionary<string, int> troops, bool atWar, SupplyConsumption result)
    {
        if (troops == null || troops.Count == 0)
            return;
        var lord = MBObjectManager.Instance.GetObject<Hero>(heroId);
        var roster = lord?.PartyBelongedTo?.MemberRoster;
        if (roster == null)
        {
            _logger.LogWarning($"[SupplyLines] Consume: lord '{heroId}' has no party roster, nothing taken");
            return;
        }

        foreach (var pair in troops)
        {
            if (pair.Value <= 0)
                continue;
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
            if (troop == null)
            {
                _logger.LogWarning($"[SupplyLines] Consume: unknown troop id '{pair.Key}' skipped");
                continue;
            }
            int have = roster.GetTroopCount(troop);
            int take = Math.Min(pair.Value, have);
            if (take <= 0)
                continue;
            roster.AddToCounts(troop, -take);
            result.Troops[pair.Key] = take;
            result.TroopRecruitCost += take * TroopUnitPrice(troop, atWar);
        }
    }

    private void ConsumeVolunteersFromSettlement(
        Settlement settlement, IReadOnlyDictionary<string, int> troops, bool atWar, SupplyConsumption result)
    {
        if (troops == null || troops.Count == 0)
            return;

        foreach (var pair in troops)
        {
            if (pair.Value <= 0)
                continue;
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
            if (troop == null)
            {
                _logger.LogWarning($"[SupplyLines] Consume: unknown troop id '{pair.Key}' skipped");
                continue;
            }

            int wanted = pair.Value;
            int taken = 0;
            ForEachRecruitableSlot(settlement, (notable, slotIndex, slotTroop) =>
            {
                if (taken >= wanted || slotTroop != troop)
                    return false;
                taken++;
                return true; // consume this slot
            });

            if (taken <= 0)
                continue;
            result.Troops[pair.Key] = taken;
            result.TroopRecruitCost += taken * TroopUnitPrice(troop, atWar);
        }
    }

    /// <summary>
    /// Walks every volunteer slot the player is actually allowed to recruit, honouring the
    /// VolunteerModel index gate per notable (TAOM's alignment gate returns -1 for "nothing").
    /// The visitor returns true to consume the slot (it is nulled), false to leave it.
    /// </summary>
    private static void ForEachRecruitableSlot(
        Settlement settlement, Func<Hero, int, CharacterObject, bool> visit)
    {
        var buyer = Hero.MainHero;
        var model = Campaign.Current?.Models?.VolunteerModel;
        if (buyer == null || model == null || settlement.Notables == null)
            return;

        foreach (var notable in settlement.Notables)
        {
            var slots = notable?.VolunteerTypes;
            if (slots == null)
                continue;
            int maxIndex = model.MaximumIndexHeroCanRecruitFromHero(buyer, notable);
            if (maxIndex < 0)
                continue; // alignment gate: this notable offers the player nothing
            int upper = Math.Min(maxIndex, slots.Length - 1);
            for (int i = 0; i <= upper; i++)
            {
                var troop = slots[i];
                if (troop == null)
                    continue;
                if (visit(notable, i, troop))
                    slots[i] = null;
            }
        }
    }

    // --- pricing helpers ---

    private int TroopUnitPrice(CharacterObject troop, bool atWar)
    {
        float vanillaCost = 0f;
        var wageModel = Campaign.Current?.Models?.PartyWageModel;
        if (wageModel != null && Hero.MainHero != null)
        {
            var explained = wageModel.GetTroopRecruitmentCost(troop, Hero.MainHero);
            // Positive requirement so a NaN from a broken model resolves to a zero base, never a
            // poisoned price.
            if (FiniteFloatValidator.IsFinite(explained.ResultNumber) && explained.ResultNumber > 0f)
                vanillaCost = explained.ResultNumber;
        }
        return _pricing.TroopPrice((int)Math.Round(vanillaCost), troop.Tier, atWar);
    }

    private static int GetItemMarketValue(Settlement settlement, ItemObject item)
    {
        // Town covers both towns and castles; fall back to the item's base value elsewhere.
        if (settlement?.Town == null)
            return item?.Value ?? 0;
        return settlement.Town.GetItemPrice(item, MobileParty.MainParty);
    }

    private static bool IsPlayerAtWarWithAnyKingdom()
    {
        var faction = Hero.MainHero?.MapFaction;
        if (faction == null || Campaign.Current == null)
            return false;
        foreach (var other in Campaign.Current.Factions)
        {
            if (other != faction && other.IsKingdomFaction && other.IsAtWarWith(faction))
                return true;
        }
        return false;
    }

    // --- eligibility helpers ---

    private List<SupplySourceInfo> GetLordSources(Hero mainHero)
    {
        var lords = new List<SupplySourceInfo>();
        string lordLabel = new TextObject("{=taom_sl_rel_lord}Lord").ToString();
        foreach (var party in MobileParty.All)
        {
            if (party == null || party.IsMainParty || !party.IsLordParty)
                continue;
            var leader = party.LeaderHero;
            if (leader == null || leader == mainHero || leader.MapFaction != mainHero.MapFaction)
                continue;
            if (party.MemberRoster == null || party.MemberRoster.TotalManCount <= 1)
                continue;
            float distance = DistanceToLord(leader);
            if (!(distance <= LordMessengerRadius))
                continue; // positive requirement: NaN or MaxValue distance excludes the lord
            lords.Add(new SupplySourceInfo
            {
                HeroId = leader.StringId,
                DisplayName = leader.Name?.ToString() ?? leader.StringId,
                RelationText = lordLabel,
                Distance = distance,
                CanOrder = true,
            });
        }
        lords.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return lords;
    }

    private static string RelationLabel(Settlement settlement, Hero mainHero, bool atWar)
    {
        if (atWar)
            return new TextObject("{=taom_sl_rel_enemy}Enemy").ToString();
        if (mainHero.Clan != null && settlement.OwnerClan == mainHero.Clan)
            return new TextObject("{=taom_sl_rel_own}Own").ToString();
        if (settlement.OwnerClan?.MapFaction == mainHero.MapFaction)
            return new TextObject("{=taom_sl_rel_allied}Allied").ToString();
        return new TextObject("{=taom_sl_rel_neutral}Neutral").ToString();
    }

    private static float DistanceToSettlement(Settlement settlement)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null || settlement == null)
            return float.MaxValue;
        try
        {
            float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(
                mainParty, settlement, isTargetingPort: false, MobileParty.NavigationType.Default, out _);
            return FiniteFloatValidator.IsFinite(distance) ? distance : float.MaxValue;
        }
        catch (Exception)
        {
            return float.MaxValue;
        }
    }

    private static float DistanceToLord(Hero lord)
    {
        var lordParty = lord?.PartyBelongedTo;
        var mainParty = MobileParty.MainParty;
        if (lordParty == null || mainParty == null)
            return float.MaxValue;
        try
        {
            float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(
                lordParty, mainParty, MobileParty.NavigationType.Default, out _);
            return FiniteFloatValidator.IsFinite(distance) ? distance : float.MaxValue;
        }
        catch (Exception)
        {
            return float.MaxValue;
        }
    }
}
