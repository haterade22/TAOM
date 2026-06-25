using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary.Domain;

namespace TAOM.Features.EliteEmissary.Hooks;

/// <summary>
/// Boundary presentation for the emissary purchase flow (engine-coupled — kept out of the pure
/// service per ADR-002): builds the two-step troop-then-quantity <c>ShowMultiSelectionInquiry</c>
/// (afford-gray per element), then delegates the decision + transaction to
/// <see cref="IEliteEmissaryService.Purchase"/> and surfaces the typed result as a player message.
/// </summary>
public sealed class EliteEmissaryInquiryPresenter
{
    private readonly IEliteEmissaryService _service;
    private readonly ISettlementOwnerAdapter _ownerAdapter;
    private readonly IModLogger _logger;

    public EliteEmissaryInquiryPresenter(IEliteEmissaryService service, ISettlementOwnerAdapter ownerAdapter, IModLogger logger)
    {
        _service = service;
        _ownerAdapter = ownerAdapter;
        _logger = logger;
    }

    public void OpenTroopList(Settlement settlement)
    {
        try
        {
            if (settlement == null) return;
            var heroId = Hero.MainHero?.StringId;
            if (heroId == null)
            {
                _logger.LogWarning("[EliteEmissary] OpenTroopList: no main hero");
                return;
            }

            var owner = _ownerAdapter.GetOwnerInfo(settlement);
            _logger.LogInfo($"[EliteEmissary] OpenTroopList at {owner.SettlementId}: owner kingdom='{owner.OwnerKingdomId}' culture='{owner.OwnerCultureId}'");

            var offerList = _service.BuildOfferList(heroId, owner.OwnerKingdomId, owner.OwnerCultureId);
            if (offerList.NoResource)
            {
                Notify("{=taom_emissary_no_resource}There is no emissary trade in this settlement.");
                return;
            }
            if (!offerList.HasOffers)
            {
                Notify("{=taom_emissary_no_offers}The emissary has no elite units to offer right now.");
                return;
            }

            var elements = new List<InquiryElement>();
            foreach (var offer in offerList.Offers)
            {
                var character = MBObjectManager.Instance?.GetObject<CharacterObject>(offer.TroopId);
                if (character == null)
                {
                    _logger.LogWarning($"[EliteEmissary] offer '{offer.TroopId}' did not resolve to a CharacterObject — skipped from list");
                    continue;
                }

                var title = new TextObject("{=taom_emissary_offer_line}{NAME} — {COST} {RESOURCE}")
                    .SetTextVariable("NAME", character.Name)
                    .SetTextVariable("COST", offer.MerchantCost)
                    .SetTextVariable("RESOURCE", offerList.ResourceDisplayName)
                    .ToString();

                var hint = (offer.CanAfford
                    ? new TextObject("{=taom_emissary_offer_afford}You can afford up to {MAX}.").SetTextVariable("MAX", offer.MaxAffordableQuantity)
                    : new TextObject("{=taom_emissary_offer_cant}Not enough {RESOURCE}.").SetTextVariable("RESOURCE", offerList.ResourceDisplayName))
                    .ToString();

                // Per-element portraits are skipped — v1.4.6's InquiryElement image type is the abstract
                // TaleWorlds.Core.ImageIdentifiers.ImageIdentifier; name + cost in the title is the safe
                // text-only baseline (QuickActions does the same). Portraits can be added later.
                elements.Add(new InquiryElement(offer, title, null, offer.CanAfford, hint));
            }

            if (elements.Count == 0)
            {
                Notify("{=taom_emissary_no_offers}The emissary has no elite units to offer right now.");
                return;
            }

            var desc = new TextObject("{=taom_emissary_list_desc}Balance: {AMOUNT} {RESOURCE}")
                .SetTextVariable("AMOUNT", (int)offerList.PlayerBalance)
                .SetTextVariable("RESOURCE", offerList.ResourceDisplayName)
                .ToString();

            var data = new MultiSelectionInquiryData(
                titleText: new TextObject("{=taom_emissary_list_title}Elite Units").ToString(),
                descriptionText: desc,
                inquiryElements: elements,
                isExitShown: true,
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=taom_emissary_select}Select").ToString(),
                negativeText: new TextObject("{=taom_emissary_back}Back").ToString(),
                affirmativeAction: chosen =>
                {
                    if (chosen == null || chosen.Count == 0) return;
                    if (chosen[0].Identifier is EmissaryTroopOffer offer)
                        OpenQuantityPicker(settlement, owner, heroId, offer, offerList.ResourceDisplayName, offerList.PlayerBalance);
                },
                negativeAction: _ => { });

            MBInformationManager.ShowMultiSelectionInquiry(data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EliteEmissary] OpenTroopList failed: {ex.Message}");
        }
    }

    private void OpenQuantityPicker(Settlement settlement, SettlementOwnerInfo owner, string heroId, EmissaryTroopOffer offer, string resourceName, float balance)
    {
        try
        {
            var character = MBObjectManager.Instance?.GetObject<CharacterObject>(offer.TroopId);
            var troopName = character?.Name?.ToString() ?? offer.TroopId;

            var quantities = new List<int>();
            foreach (var q in new[] { 1, 5, 10, offer.MaxAffordableQuantity })
                if (q >= 1 && !quantities.Contains(q))
                    quantities.Add(q);
            quantities.Sort();

            var elements = new List<InquiryElement>();
            foreach (var q in quantities)
            {
                var enabled = q <= offer.MaxAffordableQuantity;
                var label = new TextObject("{=taom_emissary_qty_line}{QTY} (×{COST} = {TOTAL} {RESOURCE})")
                    .SetTextVariable("QTY", q)
                    .SetTextVariable("COST", offer.MerchantCost)
                    .SetTextVariable("TOTAL", q * offer.MerchantCost)
                    .SetTextVariable("RESOURCE", resourceName)
                    .ToString();
                var hint = enabled ? string.Empty
                    : new TextObject("{=taom_emissary_offer_cant}Not enough {RESOURCE}.").SetTextVariable("RESOURCE", resourceName).ToString();
                elements.Add(new InquiryElement(q, label, null, enabled, hint));
            }

            var title = new TextObject("{=taom_emissary_qty_title}How many {TROOP}?").SetTextVariable("TROOP", troopName).ToString();
            var desc = new TextObject("{=taom_emissary_qty_desc}Each costs {COST} {RESOURCE}. You have {BALANCE}.")
                .SetTextVariable("COST", offer.MerchantCost)
                .SetTextVariable("RESOURCE", resourceName)
                .SetTextVariable("BALANCE", (int)balance)
                .ToString();

            var data = new MultiSelectionInquiryData(
                titleText: title,
                descriptionText: desc,
                inquiryElements: elements,
                isExitShown: true,
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=taom_emissary_confirm}Recruit").ToString(),
                negativeText: new TextObject("{=taom_emissary_back}Back").ToString(),
                affirmativeAction: chosen =>
                {
                    if (chosen == null || chosen.Count == 0) return;
                    if (chosen[0].Identifier is int qty)
                        ExecutePurchase(owner, heroId, offer.TroopId, qty);
                },
                negativeAction: _ => { });

            MBInformationManager.ShowMultiSelectionInquiry(data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EliteEmissary] OpenQuantityPicker failed: {ex.Message}");
        }
    }

    private void ExecutePurchase(SettlementOwnerInfo owner, string heroId, string troopId, int qty)
    {
        var result = _service.Purchase(heroId, owner.OwnerKingdomId, owner.OwnerCultureId, troopId, qty);
        var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        var troopName = character?.Name?.ToString() ?? troopId;

        switch (result.Status)
        {
            case EmissaryPurchaseStatus.Success:
                Notify(new TextObject("{=taom_emissary_bought}Recruited {QTY} {TROOP} for {COST} {RESOURCE}.")
                    .SetTextVariable("QTY", result.Quantity)
                    .SetTextVariable("TROOP", troopName)
                    .SetTextVariable("COST", result.TotalCost)
                    .SetTextVariable("RESOURCE", result.ResourceDisplayName).ToString(), Colors.Green);
                break;
            case EmissaryPurchaseStatus.Unaffordable:
                Notify(new TextObject("{=taom_emissary_cant_afford}Not enough {RESOURCE} — need {COST}.")
                    .SetTextVariable("RESOURCE", result.ResourceDisplayName)
                    .SetTextVariable("COST", result.TotalCost).ToString(), Colors.Red);
                break;
            case EmissaryPurchaseStatus.NoResource:
                Notify("{=taom_emissary_no_resource}There is no emissary trade in this settlement.");
                break;
            default:
                Notify("{=taom_emissary_failed}The emissary could not complete the deal.");
                break;
        }
    }

    private static void Notify(string textKeyOrLiteral) => Notify(textKeyOrLiteral, Colors.White);

    private static void Notify(string text, Color color) =>
        InformationManager.DisplayMessage(new InformationMessage(
            text.StartsWith("{=") ? new TextObject(text).ToString() : text, color));
}
