namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class ItemPoolEntry
{
    public string ItemId { get; }
    public float Weight { get; }

    public ItemPoolEntry(string itemId, float weight)
    {
        ItemId = itemId;
        Weight = weight;
    }
}
