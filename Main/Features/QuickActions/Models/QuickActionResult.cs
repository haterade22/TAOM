namespace TAOM.Features.QuickActions.Models;

public enum QuickActionStatus
{
    Success,
    NoInventoryActive,
    NothingMatched,
    Cancelled,
    Failed,
}

public readonly struct QuickActionResult
{
    public QuickActionStatus Status { get; }
    public int ItemsAffected { get; }
    public int TotalGold { get; }

    public QuickActionResult(QuickActionStatus status, int itemsAffected, int totalGold)
    {
        Status = status;
        ItemsAffected = itemsAffected;
        TotalGold = totalGold;
    }

    public static QuickActionResult Empty(QuickActionStatus status) => new(status, 0, 0);
    public static QuickActionResult OfSuccess(int items, int gold) => new(QuickActionStatus.Success, items, gold);
}
