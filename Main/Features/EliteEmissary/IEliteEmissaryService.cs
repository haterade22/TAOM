using TAOM.Features.EliteEmissary.Domain;

namespace TAOM.Features.EliteEmissary;

/// <summary>
/// Pure logic for the Settlement Elite Emissary: deciding which settlements host an emissary, what a
/// faction offers, whether the player can afford it, and executing the resource-for-troops purchase.
/// Speaks only strings + Domain records — sealed TaleWorlds types stay in the behavior/adapters
/// (ADR-002/007). The resource charged is the SETTLEMENT OWNER's, resolved from the kingdom/culture
/// the behavior passes in (so conquest flips both the list and the currency).
/// </summary>
public interface IEliteEmissaryService
{
    /// <summary>MCM master toggle — re-checked on every menu open.</summary>
    bool IsEnabled { get; }

    /// <summary>True if <paramref name="settlementId"/> is one of the curated key settlements.</summary>
    bool IsKeySettlement(string settlementId);

    /// <summary>True if the owner culture has at least one sellable (merchant_cost &gt; 0) offer AND
    /// the faction maps to a special resource — drives the "purchase elite units" dialog visibility.</summary>
    bool HasPurchasableOffers(string ownerKingdomId, string ownerCultureId);

    /// <summary>Builds the offer list for the settlement OWNER faction, with the player's per-troop
    /// affordability pre-computed. Returns <see cref="EmissaryOfferList.NoResourceAvailable"/> when
    /// the faction maps to no resource.</summary>
    EmissaryOfferList BuildOfferList(string heroId, string ownerKingdomId, string ownerCultureId);

    /// <summary>Executes a purchase: afford-check → grant troops → charge the owner resource (grant
    /// before charge, so a failed grant never charges). Never throws — returns a typed result.</summary>
    EmissaryPurchaseResult Purchase(string heroId, string ownerKingdomId, string ownerCultureId, string troopId, int quantity);
}
