using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;

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
        if (_resourceService.ResolveResource(ownerKingdomId, ownerCultureId) == null)
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

        var balance = _resourceService.GetCurrentAmount(heroId, ownerKingdomId, ownerCultureId);
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

            var maxAffordable = (int)(balance / merchantCost);
            offers.Add(new EmissaryTroopOffer(troopId, merchantCost, canAfford: balance >= merchantCost, maxAffordableQuantity: maxAffordable));
        }

        _logger.LogInfo($"[EliteEmissary] Offer list for culture '{ownerCultureId}': {offers.Count} troop(s), resource={resource.DisplayName}, balance={balance:F0}");
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
        if (merchantCost <= 0 || !IsOfferedBy(ownerCultureId, troopId))
        {
            _logger.LogWarning($"[EliteEmissary] Purchase rejected (NotOffered): '{troopId}' not a merchant offer for culture '{ownerCultureId}'");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NotOffered, troopId, quantity);
        }

        var totalCost = merchantCost * quantity;

        if (!_resourceService.CanAffordMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity))
        {
            _logger.LogInfo($"[EliteEmissary] Purchase blocked (Unaffordable): {troopId} x{quantity} costs {totalCost} {resource.DisplayName}");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Unaffordable, troopId, quantity, totalCost, resource.DisplayName);
        }

        // Grant BEFORE charge: a failed grant (no party / unknown troop id) then never charges.
        if (!_party.GrantTroop(troopId, quantity))
        {
            _logger.LogError($"[EliteEmissary] Purchase failed (grant): could not add {troopId} x{quantity} to party — no charge applied");
            return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Failed, troopId, quantity, totalCost, resource.DisplayName);
        }

        _resourceService.ChargeMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
        _logger.LogInfo($"[EliteEmissary] PURCHASE: {troopId} x{quantity} for {totalCost} {resource.DisplayName} (settlement faction kingdom='{ownerKingdomId}' culture='{ownerCultureId}')");
        return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, troopId, quantity, totalCost, resource.DisplayName);
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
