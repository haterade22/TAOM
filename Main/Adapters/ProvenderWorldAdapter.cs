using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp;
using TAOM.Features.Provender.Models;

namespace TAOM.Adapters;

/// <summary>
/// The campaign side of the Provender-master (#908, docs/features/provender.md). It reads the main party and one settlement into
/// plain facts and makes the plain writes the service asks for; it decides nothing. It reads the officer from the Quartermaster
/// ROLE (<c>GetRoleHolder</c>, never <c>EffectiveQuartermaster</c>, which falls back to the leader) and keeps no engine object
/// between calls (a process singleton). Main thread only. Every member that reads or writes the campaign catches and logs, so
/// a fault reads as "nothing there"; the registry checks and the conversation partner are null-guarded reads.
/// </summary>
public sealed class ProvenderWorldAdapter : IProvenderWorldAdapter
{
    private readonly ICampService _camps;
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;

    public ProvenderWorldAdapter(ICampService camps, IRaceManager races, IModLogger logger)
    {
        _camps = camps;
        _races = races;
        _logger = logger;
    }

    public bool IsRegistryReady => Campaign.Current != null && MBObjectManager.Instance != null;

    public bool IsKnownItem(string itemId) => FindItem(itemId) != null;

    public bool IsKnownCulture(string cultureId) =>
        !string.IsNullOrEmpty(cultureId) && MBObjectManager.Instance?.GetObject<CultureObject>(cultureId) != null;

    public string ItemName(string itemId) => FindItem(itemId)?.Name?.ToString() ?? itemId;

    public string? ConversationPartnerId => Campaign.Current?.ConversationManager?.OneToOneConversationHero?.StringId;

    public string? QuartermasterId
    {
        get
        {
            try
            {
                return Campaign.Current?.MainParty?.GetRoleHolder(PartyRole.Quartermaster)?.StringId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Provender] QuartermasterId failed: {ex.Message}");
                return null;
            }
        }
    }

    public ProvenderPartyFacts ReadParty()
    {
        try
        {
            var main = Campaign.Current?.MainParty;
            var player = Hero.MainHero;
            // A captive player's party is inactive: no order runs and no talk is offered until the party is back.
            if (main == null || player == null || !main.IsActive || player.IsPrisoner)
                return ProvenderPartyFacts.None;

            var officer = main.GetRoleHolder(PartyRole.Quartermaster);
            var heldFood = ReadHeldFood(main.ItemRoster);
            var camp = _camps.PlayerCamp;
            return new ProvenderPartyFacts
            {
                HasParty = true,
                OfficerId = officer?.StringId,
                OfficerIsPlayer = officer != null && officer == player,
                OfficerInParty = officer != null && officer.PartyBelongedTo == main && officer.IsAlive && !officer.IsPrisoner,
                Eaters = main.Party.NumberOfAllMembers + main.Party.NumberOfPrisoners / 2,
                Gold = player.Gold,
                Troops = Math.Max(main.MemberRoster.TotalManCount - 1, 0),
                SettlementId = main.CurrentSettlement?.StringId,
                EdibleFood = EdibleUnits(heldFood, main.Party.RemainingFoodPercentage),
                DailyWage = main.TotalWage,
                FoodPerDay = -main.FoodChange,
                HeldFood = heldFood,
                Livestock = ReadLivestock(main.ItemRoster),
                RaceCounts = ReadRaceCounts(main.MemberRoster),
                LeaderCulture = player.Culture?.StringId,
                CampReady = camp != null && camp.IsReady,
                InEncounter = PlayerEncounter.Current != null || main.MapEvent != null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] ReadParty failed: {ex.Message}");
            return ProvenderPartyFacts.None;
        }
    }

    public ProvenderSettlementFacts ReadSettlement(string settlementId)
    {
        try
        {
            var settlement = FindSettlement(settlementId);
            var main = Campaign.Current?.MainParty;
            if (settlement == null || main == null)
                return ProvenderSettlementFacts.None;

            var market = MarketTown(settlement);
            var offers = new List<FoodOffer>();
            if (market != null)
            {
                foreach (var element in settlement.ItemRoster)
                {
                    var item = element.EquipmentElement.Item;
                    if (item == null || !item.IsFood || element.Amount <= 0 || element.EquipmentElement.ItemModifier != null)
                        continue;
                    offers.Add(new FoodOffer(item.StringId, market.GetItemPrice(element.EquipmentElement, main, false), element.Amount));
                }
            }
            var playerFaction = Hero.MainHero?.MapFaction;
            return new ProvenderSettlementFacts
            {
                Found = true,
                IsTown = settlement.IsTown,
                IsVillage = settlement.IsVillage,
                IsCastle = settlement.IsCastle,
                IsDisturbed = settlement.Village != null
                    && (settlement.Village.VillageState != Village.VillageStates.Normal || settlement.IsUnderRaid),
                RefusesPlayer = RefusesPlayer(settlement),
                AtWarWithPlayer = playerFaction != null && settlement.MapFaction != null && settlement.MapFaction.IsAtWarWith(playerFaction),
                HasMarketTown = market != null,
                Offers = offers,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] ReadSettlement('{settlementId}') failed: {ex.Message}");
            return ProvenderSettlementFacts.None;
        }
    }

    public PurchaseResult Buy(string settlementId, string itemId, int count)
    {
        try
        {
            var settlement = FindSettlement(settlementId);
            var main = Campaign.Current?.MainParty;
            var player = Hero.MainHero;
            var market = settlement == null ? null : MarketTown(settlement);
            if (market == null || main == null || player == null || count <= 0 || !TryFindStack(settlement!.ItemRoster, itemId, out var element))
                return PurchaseResult.None;

            // SellItemsAction checks neither the stock nor the buyer's gold, so the call is sized to both here. One call per type:
            // each call the player pays shows a gold notice and a coin sound. The price can rise a little with each unit inside
            // the call; GiveGoldAction never takes more gold than the player holds, so an overrun is a small discount, never a debt.
            var price = market.GetItemPrice(element.EquipmentElement, main, false);
            if (price <= 0)
                return PurchaseResult.None;
            var units = Math.Min(Math.Min(count, element.Amount), player.Gold / price);
            if (units <= 0)
                return PurchaseResult.None;

            var before = player.Gold;
            SellItemsAction.Apply(settlement.Party, main.Party, new ItemRosterElement(element.EquipmentElement, units), units, settlement);
            return new PurchaseResult(units, Math.Max(before - player.Gold, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] Buy('{settlementId}', '{itemId}', {count}) failed: {ex.Message}");
            return PurchaseResult.None;
        }
    }

    public IReadOnlyList<int> UnitPrices(string settlementId, string itemId, int count)
    {
        try
        {
            var settlement = FindSettlement(settlementId);
            var main = Campaign.Current?.MainParty;
            var market = settlement == null ? null : MarketTown(settlement);
            if (market == null || main == null || count <= 0 || !TryFindStack(settlement!.ItemRoster, itemId, out var element))
                return Array.Empty<int>();

            var prices = new int[Math.Min(count, element.Amount)];
            // A village's sale moves no price (Village keeps SettlementComponent's empty inventory hook), so every unit costs
            // the first. A town's TownMarketData lowers the category's InStoreValue by the item's value with each unit that
            // leaves, and SellItemsAction prices every unit afresh: the same model call over the falling store, which falls in
            // int arithmetic as ItemData.AddInStore keeps it and only then becomes the model's float.
            if (!settlement.IsTown)
            {
                var flat = market.GetItemPrice(element.EquipmentElement, main, false);
                for (var k = 0; k < prices.Length; k++)
                    prices[k] = flat;
                return prices;
            }

            var item = element.EquipmentElement.Item;
            var data = market.MarketData.GetCategoryData(item.GetItemCategory());
            var model = Campaign.Current!.Models.TradeItemPriceFactorModel;
            for (var k = 0; k < prices.Length; k++)
                prices[k] = model.GetPrice(element.EquipmentElement, main, null, false, (float)(data.InStoreValue - (long)k * item.Value), data.Supply, data.Demand);
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] UnitPrices('{settlementId}', '{itemId}', {count}) failed: {ex.Message}");
            return Array.Empty<int>();
        }
    }

    public int Slaughter(string itemId, int heads)
    {
        var done = 0;
        try
        {
            var main = Campaign.Current?.MainParty;
            if (main == null || heads <= 0)
                return 0;
            for (; done < heads; done++)
            {
                if (!TryFindStack(main.ItemRoster, itemId, out var element) || element.EquipmentElement.Item.HorseComponent is not { } horse)
                    break;
                // The yield of the inventory screen's own slaughter (InventoryLogic.SlaughterItem): meat, then hides.
                main.ItemRoster.AddToCounts(element.EquipmentElement, -1);
                main.ItemRoster.AddToCounts(DefaultItems.Meat, horse.MeatCount);
                if (horse.HideCount > 0)
                    main.ItemRoster.AddToCounts(DefaultItems.Hides, horse.HideCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] Slaughter('{itemId}', {heads}) failed after {done} head(s): {ex.Message}");
        }
        return done;
    }

    public int PayTroops(int gold)
    {
        try
        {
            var player = Hero.MainHero;
            if (player == null || gold <= 0)
                return 0;
            var before = player.Gold;
            GiveGoldAction.ApplyBetweenCharacters(player, null, gold);
            return Math.Max(before - player.Gold, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] PayTroops({gold}) failed: {ex.Message}");
            return 0;
        }
    }

    public void AddMorale(int morale, bool disorganize)
    {
        try
        {
            var main = Campaign.Current?.MainParty;
            if (main == null)
                return;
            main.RecentEventsMorale += morale;
            if (disorganize)
                main.SetDisorganized(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] AddMorale({morale}, {disorganize}) failed: {ex.Message}");
        }
    }

    public void CommitPartyScreen() => PartyScreenCommit.CommitIfMainParty(_logger, "Provender");

    public void Announce(ProvenderAnnouncement kind, int count, int gold)
    {
        try
        {
            TextObject text = kind switch
            {
                ProvenderAnnouncement.BoughtFood => new TextObject("{=taom_provender_msg_food}Your Provender-master bought {COUNT} food for {GOLD} gold."),
                ProvenderAnnouncement.Slaughtered => new TextObject("{=taom_provender_msg_slaughter}Your Provender-master slaughtered {COUNT} head of livestock for meat."),
                ProvenderAnnouncement.PaidTroops => new TextObject("{=taom_provender_msg_paid}You paid your soldiers {GOLD} gold. Morale rises by {COUNT}."),
                ProvenderAnnouncement.Sold => new TextObject("{=taom_provender_msg_sold}Your Provender-master sold {COUNT} of your goods for {GOLD} gold."),
                _ => new TextObject("{=taom_provender_msg_drank}The host shares out {COUNT} of the stores. Spirits rise, but the camp falls into disorder."),
            };
            text.SetTextVariable("COUNT", count);
            text.SetTextVariable("GOLD", gold);
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] Announce({kind}) failed: {ex.Message}");
        }
    }

    /// <summary>
    /// The town whose market sets the prices: a town itself, or a village's trade town. Never a castle. A castle's village with
    /// no trade town (none in range at peace) is skipped: vanilla's trade screen would price it from the nearest town and
    /// SellItemsAction from the castle's own market, so there is no one vanilla price to quote.
    /// </summary>
    private static Town? MarketTown(Settlement settlement)
    {
        if (settlement.IsCastle)
            return null;
        if (settlement.IsTown)
            return settlement.Town;
        return settlement.Village?.TradeBound?.Town;
    }

    /// <summary>
    /// The player lacks full access: vanilla's own access model (the same call the gate menu makes), for a town or a village
    /// only, since any other settlement type asserts there. A village also refuses when the model refuses Trade there (a Hostile
    /// realm's village at peace, Hostile Peace block C), so the Provender-master does not buy where the player may not trade.
    /// </summary>
    internal static bool RefusesPlayer(Settlement settlement)
    {
        if (!settlement.IsTown && !settlement.IsVillage)
            return false;
        var model = Campaign.Current!.Models.SettlementAccessModel;
        model.CanMainHeroEnterSettlement(settlement, out var access);
        if (access.AccessLevel != SettlementAccessModel.AccessLevel.FullAccess)
            return true;
        return settlement.IsVillage
            && !model.CanMainHeroDoSettlementAction(settlement, SettlementAccessModel.SettlementAction.Trade, out _, out _);
    }

    /// <summary>The food units in the stores without the herd: the food items plus the part-eaten unit the engine carries.</summary>
    private static float EdibleUnits(Dictionary<string, int> heldFood, int remainingFoodPercentage)
    {
        long units = 0;
        foreach (var count in heldFood.Values)
            units += count;
        return units + remainingFoodPercentage / 100f;
    }

    private static Settlement? FindSettlement(string settlementId) =>
        string.IsNullOrEmpty(settlementId) || MBObjectManager.Instance == null ? null : Settlement.Find(settlementId);

    private static ItemObject? FindItem(string itemId) =>
        string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

    /// <summary>The first unmodified stack of the item; modified stacks (a rotten or fine variant) are left alone.</summary>
    private static bool TryFindStack(ItemRoster roster, string itemId, out ItemRosterElement found)
    {
        foreach (var element in roster)
        {
            var item = element.EquipmentElement.Item;
            if (item != null && element.Amount > 0 && element.EquipmentElement.ItemModifier == null && item.StringId == itemId)
            {
                found = element;
                return true;
            }
        }
        found = default;
        return false;
    }

    private static Dictionary<string, int> ReadHeldFood(ItemRoster roster)
    {
        var held = new Dictionary<string, int>();
        foreach (var element in roster)
        {
            var item = element.EquipmentElement.Item;
            if (item == null || !item.IsFood || element.Amount <= 0)
                continue;
            held.TryGetValue(item.StringId, out var have);
            held[item.StringId] = have + element.Amount;
        }
        return held;
    }

    private static List<LivestockStack> ReadLivestock(ItemRoster roster)
    {
        var stacks = new List<LivestockStack>();
        foreach (var element in roster)
        {
            var item = element.EquipmentElement.Item;
            var horse = item?.HorseComponent;
            if (horse == null || !horse.IsLiveStock || element.Amount <= 0 || element.EquipmentElement.ItemModifier != null)
                continue;
            stacks.Add(new LivestockStack(item!.StringId, element.Amount, horse.MeatCount));
        }
        return stacks;
    }

    private Dictionary<string, int> ReadRaceCounts(TroopRoster roster)
    {
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var troop = element.Character;
            if (troop == null || troop.IsPlayerCharacter || element.Number <= 0 || !_races.IsValidRaceId(troop.Race))
                continue;
            var name = _races.GetRaceNameFromId(troop.Race);
            if (string.IsNullOrEmpty(name))
                continue;
            counts.TryGetValue(name, out var have);
            counts[name] = have + element.Number;
        }
        return counts;
    }
}
