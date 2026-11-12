namespace TAOM.Features.EliteEmissary.Domain;

/// <summary>One purchasable elite troop the faction emissary offers, with the player's affordability
/// pre-computed by the service so the boundary just renders it (afford-gray + max-quantity picker).</summary>
public sealed class EmissaryTroopOffer
{
    public string TroopId { get; }

    /// <summary>One-time price per unit: the troop's <c>EmissaryPrice</c> (<c>merchant_cost</c> times the multiplier of
    /// the troop's own resource) in the settlement faction's special resource, or its gold price under the gold-only
    /// option.</summary>
    public int UnitPrice { get; }

    /// <summary>True if the player can afford at least ONE of this troop right now.</summary>
    public bool CanAfford { get; }

    /// <summary>How many the player can buy now: floor(balance / price) and, under the gold-only option, at
    /// most the party's free slots.</summary>
    public int MaxQuantity { get; }

    /// <summary>True when the party's free slots, not the balance, set <see cref="MaxQuantity"/>.</summary>
    public bool LimitedByPartySize { get; }

    public EmissaryTroopOffer(string troopId, int unitPrice, bool canAfford, int maxQuantity, bool limitedByPartySize)
    {
        TroopId = troopId;
        UnitPrice = unitPrice;
        CanAfford = canAfford;
        MaxQuantity = maxQuantity;
        LimitedByPartySize = limitedByPartySize;
    }
}
