namespace TAOM.Features.EliteEmissary.Domain;

public enum EmissaryPurchaseStatus
{
    Success,
    /// <summary>Player lacks enough of the owner faction's resource for the requested quantity.</summary>
    Unaffordable,
    /// <summary>Owner faction maps to no special resource — nothing to charge.</summary>
    NoResource,
    /// <summary>Troop isn't in the owner culture's offer list or has no merchant_cost.</summary>
    NotOffered,
    /// <summary>Bad request (null/empty troop id, quantity ≤ 0).</summary>
    Invalid,
    /// <summary>Charge/afford passed but the roster grant failed (no party, unknown troop id).</summary>
    Failed,
}

/// <summary>Typed outcome of <see cref="IEliteEmissaryService.Purchase"/> — never throws; the boundary
/// maps the status to a player message.</summary>
public sealed class EmissaryPurchaseResult
{
    public EmissaryPurchaseStatus Status { get; }
    public string TroopId { get; }
    public int Quantity { get; }
    public int TotalCost { get; }
    public string ResourceDisplayName { get; }

    public bool IsSuccess => Status == EmissaryPurchaseStatus.Success;

    public EmissaryPurchaseResult(EmissaryPurchaseStatus status, string troopId, int quantity, int totalCost, string resourceDisplayName)
    {
        Status = status;
        TroopId = troopId;
        Quantity = quantity;
        TotalCost = totalCost;
        ResourceDisplayName = resourceDisplayName;
    }

    public static EmissaryPurchaseResult Of(EmissaryPurchaseStatus status, string troopId = null, int quantity = 0, int totalCost = 0, string resourceDisplayName = null) =>
        new(status, troopId, quantity, totalCost, resourceDisplayName);
}
