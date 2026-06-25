namespace TAOM.Features.EliteEmissary.Domain;

/// <summary>One purchasable elite troop the faction emissary offers, with the player's affordability
/// pre-computed by the service so the boundary just renders it (afford-gray + max-quantity picker).</summary>
public sealed class EmissaryTroopOffer
{
    public string TroopId { get; }

    /// <summary>One-time <c>merchant_cost</c> per unit, in the settlement faction's special resource.</summary>
    public int MerchantCost { get; }

    /// <summary>True if the player can afford at least ONE of this troop right now.</summary>
    public bool CanAfford { get; }

    /// <summary>How many the player can afford at the current balance (floor(balance / cost)).</summary>
    public int MaxAffordableQuantity { get; }

    public EmissaryTroopOffer(string troopId, int merchantCost, bool canAfford, int maxAffordableQuantity)
    {
        TroopId = troopId;
        MerchantCost = merchantCost;
        CanAfford = canAfford;
        MaxAffordableQuantity = maxAffordableQuantity;
    }
}
