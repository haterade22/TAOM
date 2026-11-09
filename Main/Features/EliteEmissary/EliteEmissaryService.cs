using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.EliteEmissary;

/// <summary>
/// Pure decision + transaction logic for the Settlement Elite Emissary (ADR-002/007 — no sealed
/// TaleWorlds types). Reuses the SpecialResources economy for resolution/balance/charge; the
/// emissary's own price lives in <c>merchant_cost</c>, kept separate from the volunteer
/// <c>recruit_cost</c> so the two economies never collide. See docs/features/elite-emissary.md.
/// </summary>
public sealed class EliteEmissaryService : IEliteEmissaryService
{
    private const string GoldPurchaseCheckId = "taom.elite-emissary.gold-purchase";
    private const string GoldLogName = "gold";

    private readonly IEliteEmissaryConfigProvider _config;
    private readonly IEliteEmissarySettingsProvider _settings;
    private readonly ISpecialResourceService _resourceService;
    private readonly ISpecialResourceConfigProvider _resourceConfig;
    private readonly IPlayerPartyAdapter _party;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ISpecialResourceSettingsProvider _goldOption;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;

    public EliteEmissaryService(
        IEliteEmissaryConfigProvider config,
        IEliteEmissarySettingsProvider settings,
        ISpecialResourceService resourceService,
        ISpecialResourceConfigProvider resourceConfig,
        IPlayerPartyAdapter party,
        IArmouryPlayerAdapter player,
        ISpecialResourceSettingsProvider goldOption,
        IOutcomeLog outcomes,
        IModLogger logger)
    {
        _config = config;
        _settings = settings;
        _resourceService = resourceService;
        _resourceConfig = resourceConfig;
        _party = party;
        _player = player;
        _goldOption = goldOption;
        _outcomes = outcomes;
        _logger = logger;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public bool IsKeySettlement(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId)) return false;
        return _config.GetConfig().KeySettlementIds.Contains(settlementId);
    }

    public bool HasPurchasableOffers(string ownerKingdomId, string ownerCultureId)
    {
        var resource = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
        if (resource == null)
            return false;

        if (_goldOption.EliteTroopsCostGoldOnly && !BuyerUsesResource(resource))
            return false;

        foreach (var troopId in GetCultureOffers(ownerCultureId))
        {
            var cost = _resourceConfig.GetTroopCost(troopId);
            if (cost != null && cost.MerchantCost > 0)
                return true;
        }
        return false;
    }

    public EmissaryOfferList BuildOfferList(string heroId, string ownerKingdomId, string ownerCultureId)
    {
        var resource = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
        if (resource == null)
        {
            _logger.LogDebug($"[EliteEmissary] BuildOfferList: no resource for kingdom='{ownerKingdomId}' culture='{ownerCultureId}'");
            return EmissaryOfferList.NoResourceAvailable;
        }

        var sellsForGold = _goldOption.EliteTroopsCostGoldOnly;
        var balance = sellsForGold ? (double)_player.Gold : _resourceService.GetCurrentAmount(heroId, ownerKingdomId, ownerCultureId);
        var offers = new List<EmissaryTroopOffer>();

        foreach (var troopId in GetCultureOffers(ownerCultureId))
        {
            var cost = _resourceConfig.GetTroopCost(troopId);
            var merchantCost = cost?.MerchantCost ?? 0;
            if (merchantCost <= 0)
            {
                _logger.LogWarning($"[EliteEmissary] Offer '{troopId}' (culture {ownerCultureId}) has no merchant_cost — skipped");
                continue;
            }

            var price = UnitPrice(merchantCost, sellsForGold);
            var maxAffordable = (int)(balance / price);
            offers.Add(new EmissaryTroopOffer(troopId, price, canAfford: balance >= price, maxAffordableQuantity: maxAffordable));
        }

        var resourceName = sellsForGold ? null : resource.DisplayName;
        _logger.LogInfo($"[EliteEmissary] Offer list for culture '{ownerCultureId}': {offers.Count} troop(s), resource={resourceName ?? GoldLogName}, balance={balance:F0}");
        return EmissaryOfferList.ForResource(resource.Id, resourceName, resource.IconSpriteName, balance, offers);
    }

    public EmissaryPurchaseResult Purchase(string heroId, string ownerKingdomId, string ownerCultureId, string troopId, int quantity)
    {
        if (string.IsNullOrEmpty(troopId) || quantity <= 0)
        {
            _logger.LogWarning($"[EliteEmissary] Purchase rejected (Invalid): troop='{troopId}' qty={quantity}");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Invalid, troopId, quantity);
        }

        var resource = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
        if (resource == null)
        {
            _logger.LogWarning($"[EliteEmissary] Purchase rejected (NoResource): kingdom='{ownerKingdomId}' culture='{ownerCultureId}'");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NoResource, troopId, quantity);
        }

        var cost = _resourceConfig.GetTroopCost(troopId);
        var merchantCost = cost?.MerchantCost ?? 0;
        if (merchantCost <= 0 || !IsOfferedBy(ownerCultureId, troopId))
        {
            _logger.LogWarning($"[EliteEmissary] Purchase rejected (NotOffered): '{troopId}' not a merchant offer for culture '{ownerCultureId}'");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NotOffered, troopId, quantity);
        }

        var sellsForGold = _goldOption.EliteTroopsCostGoldOnly;
        var exactCost = (long)UnitPrice(merchantCost, sellsForGold) * quantity;
        var totalCost = (int)Math.Min(exactCost, int.MaxValue);
        var resourceName = sellsForGold ? null : resource.DisplayName;
        var currency = resourceName ?? GoldLogName;

        var canAfford = sellsForGold
            ? _player.Gold >= exactCost
            : _resourceService.CanAffordMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
        if (!canAfford)
        {
            _logger.LogInfo($"[EliteEmissary] Purchase blocked (Unaffordable): {troopId} x{quantity} costs {totalCost} {currency}");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Unaffordable, troopId, quantity, totalCost, resourceName);
        }

        // Grant BEFORE charge: a failed grant (no party / unknown troop id) then never charges.
        if (!_party.GrantTroop(troopId, quantity))
        {
            _logger.LogError($"[EliteEmissary] Purchase failed (grant): could not add {troopId} x{quantity} to party — no charge applied");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Failed, troopId, quantity, totalCost, resourceName);
        }

        if (sellsForGold)
            ChargeGold(troopId, quantity, totalCost);
        else
            _resourceService.ChargeMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
        _logger.LogInfo($"[EliteEmissary] PURCHASE: {troopId} x{quantity} for {totalCost} {currency} (settlement faction kingdom='{ownerKingdomId}' culture='{ownerCultureId}')");
        return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, troopId, quantity, totalCost, resourceName);
    }

    private int UnitPrice(int merchantCost, bool sellsForGold) =>
        sellsForGold ? merchantCost * _goldOption.EmissaryGoldPerResource : merchantCost;

    private bool BuyerUsesResource(SpecialResource ownerResource) =>
        _resourceService.ResolveResource(_player.KingdomId, _player.CultureId)?.Id == ownerResource.Id;

    private void ChargeGold(string troopId, int quantity, int totalCost)
    {
        var detail = FormattableString.Invariant($"troop={troopId} qty={quantity} gold={totalCost}");
        if (_player.ChargeGold(totalCost))
        {
            _outcomes.Pass(GoldPurchaseCheckId, "", detail);
            return;
        }

        _logger.LogError($"[EliteEmissary] Gold charge refused after the grant: {detail}");
        _outcomes.Fail(GoldPurchaseCheckId, "", detail);
    }

    private IReadOnlyList<string> GetCultureOffers(string cultureId)
    {
        if (cultureId != null && _config.GetConfig().CultureOffers.TryGetValue(cultureId, out var list) && list != null)
            return list;
        return Array.Empty<string>();
    }

    private bool IsOfferedBy(string cultureId, string troopId)
    {
        foreach (var id in GetCultureOffers(cultureId))
            if (id == troopId)
                return true;
        return false;
    }
}
