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
    private readonly IEliteEmissaryConfigProvider _config;
    private readonly IEliteEmissarySettingsProvider _settings;
    private readonly ISpecialResourceService _resourceService;
    private readonly ISpecialResourceConfigProvider _resourceConfig;
    private readonly IPlayerPartyAdapter _party;
    private readonly IModLogger _logger;

    public EliteEmissaryService(
        IEliteEmissaryConfigProvider config,
        IEliteEmissarySettingsProvider settings,
        ISpecialResourceService resourceService,
        ISpecialResourceConfigProvider resourceConfig,
        IPlayerPartyAdapter party,
        IModLogger logger)
    {
        _config = config;
        _settings = settings;
        _resourceService = resourceService;
        _resourceConfig = resourceConfig;
        _party = party;
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
        var owner = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
        if (owner == null)
            return false;
        if (_resourceService.IsGoldMode && !BuyerSharesResource(owner))
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

        var goldMode = _resourceService.IsGoldMode;
        if (goldMode && !BuyerSharesResource(resource))
        {
            _logger.LogDebug($"[EliteEmissary] BuildOfferList: gold mode, buyer's resource differs from '{resource.Id}'");
            return EmissaryOfferList.NoResourceAvailable;
        }

        var balance = goldMode ? _party.GetMainHeroGold() : _resourceService.GetCurrentAmount(heroId, ownerKingdomId, ownerCultureId);
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

            var price = goldMode ? SpecialResourceGoldPrice.ToGold(merchantCost, _resourceService.GoldPerResourcePoint) : merchantCost;
            if (price <= 0) continue;

            var maxAffordable = (int)(balance / price);
            // A gold total past MaxGold would saturate and undercharge, so no quantity may reach past it.
            if (goldMode) maxAffordable = Math.Min(maxAffordable, SpecialResourceGoldPrice.MaxGold / price);
            offers.Add(new EmissaryTroopOffer(troopId, price, canAfford: balance >= price, maxAffordableQuantity: maxAffordable));
        }

        _logger.LogInfo($"[EliteEmissary] Offer list for culture '{ownerCultureId}': {offers.Count} troop(s), {(goldMode ? "gold" : "resource=" + resource.DisplayName)}, balance={balance:F0}");
        if (goldMode)
            return EmissaryOfferList.ForGold(balance, offers);
        return EmissaryOfferList.ForResource(resource.Id, resource.DisplayName, resource.IconSpriteName, balance, offers);
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
        var goldMode = _resourceService.IsGoldMode;
        if (merchantCost <= 0 || !IsOfferedBy(ownerCultureId, troopId) || (goldMode && !BuyerSharesResource(resource)))
        {
            _logger.LogWarning($"[EliteEmissary] Purchase rejected (NotOffered): '{troopId}' not a merchant offer for culture '{ownerCultureId}'");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NotOffered, troopId, quantity);
        }

        var currency = goldMode ? "gold" : resource.DisplayName;
        var shownName = goldMode ? null : resource.DisplayName;
        var totalCost = goldMode
            ? SpecialResourceGoldPrice.ToGold((double)merchantCost * quantity, _resourceService.GoldPerResourcePoint)
            : merchantCost * quantity;

        var affordable = goldMode
            ? _party.GetMainHeroGold() >= totalCost
              && (double)merchantCost * quantity * _resourceService.GoldPerResourcePoint <= SpecialResourceGoldPrice.MaxGold
            : _resourceService.CanAffordMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
        if (!affordable)
        {
            _logger.LogInfo($"[EliteEmissary] Purchase blocked (Unaffordable): {troopId} x{quantity} costs {totalCost} {currency}");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Unaffordable, troopId, quantity, totalCost, shownName, goldMode);
        }

        // Grant BEFORE charge: a failed grant (no party / unknown troop id) then never charges.
        if (!_party.GrantTroop(troopId, quantity))
        {
            _logger.LogError($"[EliteEmissary] Purchase failed (grant): could not add {troopId} x{quantity} to party — no charge applied");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Failed, troopId, quantity, totalCost, shownName, goldMode);
        }

        if (!goldMode)
            _resourceService.ChargeMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
        else if (!_party.TryDebitMainHeroGold(totalCost))
            _logger.LogError($"[EliteEmissary] Gold debit of {totalCost} failed after {troopId} x{quantity} was granted (the afford check passed a moment earlier)");
        _logger.LogInfo($"[EliteEmissary] PURCHASE: {troopId} x{quantity} for {totalCost} {currency} (settlement faction kingdom='{ownerKingdomId}' culture='{ownerCultureId}')");
        return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, troopId, quantity, totalCost, shownName, goldMode);
    }

    // Gold mode (#1109): the player may buy a faction's elites only when their own resolved
    // resource is that faction's, so gold does not open a faction's troops that resources never did.
    private bool BuyerSharesResource(SpecialResource owner)
    {
        var buyer = _resourceService.ResolveResource(_party.GetMainHeroKingdomId(), _party.GetMainHeroCultureId());
        return buyer != null && buyer.Id == owner.Id;
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
