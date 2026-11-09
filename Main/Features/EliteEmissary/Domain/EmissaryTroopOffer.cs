namespace TAOM.Features.EliteEmissary.Domain;

/// <summary>One purchasable elite troop the faction emissary offers, with the player's affordability
/// pre-computed by the service so the boundary just renders it (afford-gray + max-quantity picker).</summary>
public sealed class EmissaryTroopOffer
{
    public string TroopId { get; }

    /// <summary>One-time price per unit: <c>merchant_cost</c> in the settlement faction's special resource, or
    /// its gold price under the gold-only option.</summary>
    public int UnitPrice { get; }

    /// <summary>True if the player can afford at least ONE of this troop right now.</summary>
    public bool CanAfford { get; }

    /// <summary>How many the player can afford at the current balance (floor(balance / cost)).</summary>
    public int MaxAffordableQuantity { get; }

    public EmissaryTroopOffer(string troopId, int unitPrice, bool canAfford, int maxAffordableQuantity)
    {
        TroopId = troopId;
        UnitPrice = unitPrice;
        CanAfford = canAfford;
        MaxAffordableQuantity = maxAffordableQuantity;
    }
}
